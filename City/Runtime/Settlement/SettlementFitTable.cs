#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-16. ONE ROW of the derived table: what a dump of real places says about one order, in one
    /// continent bucket. Five population quantiles, three spacing quantiles, the observed extremes, and the rank-size
    /// exponent fitted within that order, with the record count the figures came from.
    ///
    /// It holds QUANTILES rather than the records themselves, and that is the whole reason a table exists instead of a
    /// shipped dump: a hundred and thirty thousand rows are useless at runtime and would sit in every build, while nine
    /// numbers per order per continent say what the shape of the distribution is. It is also the reason this is a
    /// derivative work rather than a redistribution, which is what <c>THIRD-PARTY-NOTICES.md</c> records.
    ///
    /// Quantiles, not a mean and a deviation, because the population of settlements is heavy-tailed -- that is what the
    /// rank-size law says -- and the mean of a heavy-tailed sample is dragged around by its largest member. The 5th and
    /// 95th percentiles are stable under one more megacity arriving in the dump; a mean is not. </summary>
    [Serializable]
    public sealed class SettlementFitRow
    {
        /// <summary> Which order this row describes, matching <see cref="SettlementOrderClass.orderIndex"/>: 1 capital
        /// through 4 village. </summary>
        public int orderIndex = 1;

        /// <summary> Which continent bucket, or <see cref="SettlementFitTable.WorldKey"/> for the whole dump. The keys
        /// are the tz database region prefixes the dump itself carries -- see
        /// <see cref="SettlementFitTable.ContinentKeyFromTimeZone"/> for why that field and not a country table. </summary>
        public string continentKey = SettlementFitTable.WorldKey;

        /// <summary> How many records this row was fitted from. A row fitted from a handful of places is not a
        /// measurement of anything, which is why <see cref="IsUsable"/> refuses one. </summary>
        public int recordCount;

        /// <summary> The population distribution of this order, at the 5th, 25th, 50th, 75th and 95th percentiles.
        /// The band an order finally admits is built from these by
        /// <see cref="SettlementFitTable.ApplyTo"/>, which has to make touching bands out of overlapping
        /// distributions -- the arithmetic is there, with its reasoning, rather than here. </summary>
        public int populationP05;
        public int populationP25;
        public int populationP50;
        public int populationP75;
        public int populationP95;

        /// <summary> The smallest and largest population actually seen in this order. The extremes are kept beside the
        /// quantiles because a BAND needs an outer edge and a percentile is not one: the capital order's ceiling is the
        /// largest capital the dump holds, not its 95th percentile, or the largest places in the world would fall
        /// outside every band the ladder offers.
        ///
        /// <see cref="populationObservedMin"/> carries a caveat that belongs to the source rather than to this field:
        /// <c>cities1000</c> holds places of a thousand people and up, so a fit from it CANNOT see a hamlet of two
        /// hundred. The village floor therefore stays authored, and <see cref="SettlementFitTable.ApplyTo"/> says so
        /// rather than raising the floor to what the dump happens to be able to see. </summary>
        public int populationObservedMin;
        public int populationObservedMax;

        /// <summary> The measured nearest-neighbour distance between places of this order, in metres, at the 25th, 50th
        /// and 75th percentiles. Great-circle distances, so a spacing measured across a continent is not distorted by a
        /// projection.
        ///
        /// This is the MEASUREMENT that sits beside Christaller's CITED band in the order catalogue. The two are
        /// different kinds of number and the report that applies one over the other names both, because a fit quietly
        /// overwriting a citation would leave the catalogue's own XML comment describing a figure that is no longer
        /// there. </summary>
        public float spacingP25Metres;
        public float spacingP50Metres;
        public float spacingP75Metres;

        /// <summary> The rank-size exponent fitted WITHIN this order: the slope of log population against log rank,
        /// negated, so that <c>P(r) = P(1) / r^q</c> with a positive q.
        ///
        /// FITTED, NEVER ASSUMED. That the exponent is measured rather than taken as 1 is the whole point of the import:
        /// a ladder that assumes 1 makes the second city half the first everywhere on Earth. <see cref="rankSizeR2"/> is
        /// carried beside it so a reader can see whether the straight line the fit assumes is a straight line in this
        /// data; a low R2 means the exponent is a summary of a curve and should be treated as one. </summary>
        public float rankSizeExponent = 1f;
        public float rankSizeR2;

        /// <summary> The fewest records a row may be fitted from: twenty.
        ///
        /// Not a taste: the 95th percentile of nineteen records IS its largest member, so below twenty this row's
        /// extremes and its quantiles are the same numbers wearing two names, and the band built from them is the range
        /// of a handful of places rather than the shape of a distribution. Twenty is the smallest count at which one
        /// record in twenty exists to be the 95th percentile. </summary>
        public const int MinimumRecordCount = 20;

        /// <summary> Whether this row can be used AS A FIT, with the reason when it cannot. Checked before anything is
        /// written into an order catalogue, because a fit that failed quietly is worse than no fit: the catalogue would
        /// carry <c>fittedFromData</c> true over numbers nobody measured.
        ///
        /// Two things have to hold -- the row has to be shaped like a sorted sample, and it has to have enough places
        /// behind it. They are asked separately because the shipped placeholder table has no places behind it at all and
        /// is still required to be shaped correctly; see <see cref="IsShapeConsistent"/>. </summary>
        public bool IsUsable(out string reason)
        {
            if (recordCount < MinimumRecordCount)
            {
                reason = "the fitted row for order " + orderIndex + " in " + continentKey + " was fitted from "
                         + recordCount + " places, and " + MinimumRecordCount + " is the fewest a percentile can be"
                         + " read from -- narrow the continent filter less, or fit the whole world instead";
                return false;
            }
            return IsShapeConsistent(out reason);
        }

        /// <summary> Whether the numbers in this row contradict each other, setting aside how many places produced them.
        /// Every rule here is one a sorted sample cannot break, so a failure is a fault in whatever wrote the row --
        /// which is as true of the shipped placeholders as it is of a measured fit. </summary>
        public bool IsShapeConsistent(out string reason)
        {
            string label = "the fitted row for order " + orderIndex + " in " + continentKey;
            if (populationP05 > populationP25 || populationP25 > populationP50 || populationP50 > populationP75
                || populationP75 > populationP95)
            {
                reason = label + " carries population quantiles that do not rise (" + populationP05 + ", "
                         + populationP25 + ", " + populationP50 + ", " + populationP75 + ", " + populationP95
                         + "), so they were not sorted before they were read";
                return false;
            }
            if (populationObservedMin > populationP05 || populationP95 > populationObservedMax)
            {
                reason = label + " places its quantiles outside the range it observed (" + populationObservedMin
                         + " to " + populationObservedMax + "), which cannot happen in one sorted sample";
                return false;
            }
            if (spacingP25Metres <= 0f || spacingP25Metres > spacingP50Metres
                || spacingP50Metres > spacingP75Metres)
            {
                reason = label + " carries nearest-neighbour spacings that are not positive and rising ("
                         + spacingP25Metres.ToString("0") + ", " + spacingP50Metres.ToString("0") + ", "
                         + spacingP75Metres.ToString("0") + " m)";
                return false;
            }
            if (rankSizeExponent <= 0f)
            {
                reason = label + " fitted a rank-size exponent of " + rankSizeExponent.ToString("0.###")
                         + ", and an exponent at or below zero says the larger places are the rarer ones -- which is the"
                         + " opposite of what a settlement distribution does";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    /// <summary> B-15 / C-16. THE DERIVED TABLE: what a dump of real settlements says about the four orders, as a few
    /// numbers per order per continent, and the only thing from that dump this product ships.
    ///
    /// WHAT SHIPS AND WHAT DOES NOT, because the difference is a licence question and not a size question. The dump
    /// itself -- GeoNames <c>cities1000</c>, CC-BY 4.0, some 130,000 records -- is never redistributed: the author
    /// downloads it, <c>GeoNamesImportWindow</c> reads it once, and what is written is this table. A table of quantiles
    /// is still a DERIVATIVE WORK of the dump, so attribution is still owed, and <c>THIRD-PARTY-NOTICES.md</c> carries
    /// the GeoNames entry for exactly that reason.
    ///
    /// WHY A TABLE AND NOT A DUMP, practically: a hundred and thirty thousand rows would sit in every build a game ships
    /// and be read once, and nothing downstream asks a question about a named place. What the generator needs is the
    /// SHAPE of the distribution -- how big a town is, how far towns stand apart, how fast size falls with rank -- and
    /// that is nine numbers, not a hundred thousand rows.
    ///
    /// WHAT IT DOES NOT AND CANNOT HOLD. The two allometric exponents are not here. GeoNames carries a place's
    /// population, its position, its feature code and its elevation; it carries no built-up area and no street length, so
    /// no fit of area or street length against population can be made from it. <see cref="allometryNote"/> says that in
    /// the asset itself, and the exponents stay authored in <see cref="SettlementOrderCatalog"/> where their form is
    /// cited and their value is flagged as a placeholder. Fitting them from a source that does not contain the variable
    /// would be the invented number this register exists to prevent.
    ///
    /// A SHIPPED TABLE IS NOT A FIT. <see cref="fittedFromRealDump"/> is false in the table that ships, and
    /// <see cref="ApplyTo"/> refuses to write such a table into an order catalogue -- there is nothing to apply, because
    /// the authored rows were derived from that catalogue's own authored bands in the first place. The tool runs before
    /// any import because C-15's catalogue ships runnable defaults, not because this table pretends to be a
    /// measurement. </summary>
    [CreateAssetMenu(fileName = "SettlementFitTable",
        menuName = "Road City Master/Settlement Fit Table", order = 5)]
    public sealed class SettlementFitTable : ScriptableObject
    {
        /// <summary> One row per order per continent bucket. Looked up by both keys, never by position. </summary>
        public List<SettlementFitRow> rows = new List<SettlementFitRow>();

        /// <summary> B-27 / D-6. One RAIL row per order per continent bucket: whether a settlement of that order has a
        /// railway, how many stations it has and how far apart they stand. Looked up by both keys, like the rows above.
        ///
        /// A SEPARATE LIST rather than eight more fields on <see cref="SettlementFitRow"/>, because it is fitted from a
        /// DIFFERENT FILE and can therefore be absent while the population rows are present. <c>cities1000</c> carries no
        /// railway of any kind; station presence comes from the same export's <c>allCountries.txt</c> filtered to the
        /// railway feature codes. An author who imported only the small file gets population rows and no rail rows, and
        /// <see cref="railFittedFromRealDump"/> is what says so -- exactly the distinction
        /// <see cref="allometryFitted"/> already draws for the two allometric exponents.
        ///
        /// It is on THIS table rather than in an asset of its own because the two fits share their keys, their buckets
        /// and their meaning: a rail row for order 2 in <c>Europe</c> is only interpretable beside the population row for
        /// order 2 in <c>Europe</c>, and two assets free to hold different buckets is two assets free to disagree about
        /// what a city is. </summary>
        public List<SettlementRailFitRow> railRows = new List<SettlementRailFitRow>();

        /// <summary> Whether <see cref="railRows"/> came from a real dump. False in the table that ships and false in a
        /// table whose population rows were fitted without the railway file, so a consumer asks THIS flag rather than
        /// <see cref="fittedFromRealDump"/> before drawing a railway from a fitted curve. </summary>
        public bool railFittedFromRealDump;

        /// <summary> The railway file the rail fit read, and how many station records it took from it. Separate from
        /// <see cref="dumpFileName"/> and <see cref="recordsRead"/> for the reason the list above is separate: a table
        /// can have been fitted from two files on two days, and one pair of provenance fields could only describe
        /// one. </summary>
        public string railDumpFileName = "";
        public int railStationsRead;

        /// <summary> B-27 / D-9. One UNDERGROUND row per order per continent bucket: whether a settlement of that order
        /// has an underground railway -- a metro -- how many stations it has and how far apart they stand. A THIRD list
        /// for the reason <see cref="railRows"/> is a second one: it is fitted from a different filter of the same
        /// export -- the subway and metro feature codes rather than every railway station -- so it can be present or
        /// absent independently of the surface rail rows, and <see cref="undergroundFittedFromRealDump"/> is what says
        /// which. It reuses <see cref="SettlementRailFitRow"/> unchanged, because a metro's presence, station count and
        /// station spacing are the same three fitted shapes a surface railway's are; what differs is the file the row was
        /// counted from and the far steeper presence curve, which is a difference in the row's NUMBERS, not its
        /// type. </summary>
        public List<SettlementRailFitRow> undergroundRows = new List<SettlementRailFitRow>();

        /// <summary> Whether <see cref="undergroundRows"/> came from a real dump. False in the table that ships and false
        /// in a table fitted without the metro filter, so a consumer asks THIS flag -- not
        /// <see cref="railFittedFromRealDump"/> and not <see cref="fittedFromRealDump"/> -- before drawing a metro from a
        /// fitted curve. </summary>
        public bool undergroundFittedFromRealDump;

        /// <summary> The file the metro fit read, and how many station records it took from it. Separate from the rail
        /// provenance for the same reason the rail provenance is separate from the population provenance: three fits can
        /// have read three files on three days. </summary>
        public string undergroundDumpFileName = "";
        public int undergroundStationsRead;

        /// <summary> B-27 / D-11. The LAND-USE composition rows: what fraction of a settlement's developed ground is
        /// residential, commercial, market and industrial, per order. A fourth fitted list beside the population, rail
        /// and metro rows, for the reason each of those is its own list -- it is a different measurement of the same
        /// settlements (their built area by use), fitted per order, so the shares "come from the fit, not a typed
        /// table" (D-11). </summary>
        public List<SettlementLandUseFitRow> landUseRows = new List<SettlementLandUseFitRow>();

        /// <summary> Whether <see cref="landUseRows"/> came from a real dump. False in the shipped table, whose shares
        /// are authored placeholders -- a consumer asks THIS flag before treating the composition as measured. </summary>
        public bool landUseFittedFromRealDump;

        /// <summary> Which dump these figures came from, and under what licence -- carried in the asset rather than only
        /// in a notices file, so a table that travels out of this project takes its attribution with it. </summary>
        public string sourceName = "GeoNames cities1000";
        public string sourceLicence = "CC-BY 4.0";
        public string sourceUrl = SourceUrlDefault;

        /// <summary> Where the dump is downloaded from. A const because the importer window prints the same address in
        /// its help box, and an address kept in two places is an address that goes stale in one of them. </summary>
        public const string SourceUrlDefault = "https://download.geonames.org/export/dump/";

        /// <summary> The file the fit read, the UTC day it ran, and how many records were read and used. The window
        /// shows the used count on screen so that "over a thousand real places" is a number a reader can see rather than
        /// a claim in a document. </summary>
        public string dumpFileName = "";
        public string fittedOnUtc = "";
        public int recordsRead;
        public int recordsUsed;

        /// <summary> Whether these rows came from a real dump. False in the table that ships, and
        /// <see cref="ApplyTo"/> refuses a table where it is false, so an authored placeholder can never be written into
        /// a catalogue and then quoted as a measurement. The importer sets it true as it writes. </summary>
        public bool fittedFromRealDump;

        /// <summary> Always false from a GeoNames fit, and the note says why in the asset rather than only in this
        /// file's header, because the asset is what an author looks at. </summary>
        public bool allometryFitted;
        public string allometryNote = "GeoNames carries population, position, feature code and elevation. It carries no"
                                      + " built-up area and no street length, so the two allometric exponents cannot be"
                                      + " fitted from it and remain authored in the order catalogue. Fitting them here"
                                      + " would mean inventing the variable they are fitted against.";

        /// <summary> The key for a row fitted from the whole dump, whatever continent each record sat in. Every table
        /// carries the world rows; the continent rows are additional, and a project that wants one shape everywhere can
        /// use these and nothing else. </summary>
        public const string WorldKey = "World";

        /// <summary> Which continent bucket a record belongs to, read from the dump's OWN time-zone field -- the tz
        /// database region prefix in <c>Europe/London</c> or <c>Africa/Cairo</c>.
        ///
        /// THAT FIELD AND NOT A COUNTRY TABLE, deliberately. <c>cities1000</c> has no continent column; it has a country
        /// code, and turning 250 country codes into continents means shipping a 250-row table nobody in this project
        /// authored or can check. The time zone is a field the dump already carries, its prefix is a documented part of
        /// the tz database, and the mapping is one line of string handling rather than a table with 250 chances to be
        /// wrong.
        ///
        /// ITS ONE HONEST LIMIT: the tz database does not separate North from South America -- both are
        /// <c>America/...</c> -- so this returns one America bucket and does not invent a boundary between them. A
        /// project that needs them apart can filter the dump by country code in the window. <c>Atlantic</c>,
        /// <c>Indian</c>, <c>Pacific</c> and <c>Antarctica</c> are kept as their own buckets rather than being assigned
        /// to a neighbouring continent, for the same reason. </summary>
        public static string ContinentKeyFromTimeZone(string timeZone)
        {
            if (string.IsNullOrEmpty(timeZone)) { return "Unknown"; }
            int slash = timeZone.IndexOf('/');
            if (slash <= 0) { return "Unknown"; }
            return timeZone.Substring(0, slash);
        }

        /// <summary> The row for one order in one continent bucket, or null when the table holds none. </summary>
        public SettlementFitRow Get(int orderIndex, string continentKey)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].orderIndex != orderIndex) { continue; }
                if (rows[i].continentKey == continentKey) { return rows[i]; }
            }
            return null;
        }

        /// <summary> True when a row exists for that order and bucket, returning it. </summary>
        public bool TryGet(int orderIndex, string continentKey, out SettlementFitRow row)
        {
            row = Get(orderIndex, continentKey);
            return row != null;
        }

        /// <summary> B-27 / D-6. The RAIL row for one order in one continent bucket, or null when the table holds none.
        /// Deliberately a second method rather than a flag on <see cref="Get"/>: a caller that wants the population row
        /// and a caller that wants the rail row want different types back, and a table can legitimately answer one and
        /// not the other. </summary>
        public SettlementRailFitRow GetRail(int orderIndex, string continentKey)
        {
            for (int i = 0; i < railRows.Count; i++)
            {
                if (railRows[i] == null || railRows[i].orderIndex != orderIndex) { continue; }
                if (railRows[i].continentKey == continentKey) { return railRows[i]; }
            }
            return null;
        }

        /// <summary> The rail row for that order and bucket, FALLING BACK to the world row when the bucket has none.
        /// The population lookup above has no such fallback because its caller -- <see cref="ApplyTo"/> -- is writing one
        /// named bucket's figures into the catalogue and a silent substitution there would mislabel the result. This one
        /// is asked at siting time about one settlement, where the honest answer to "no European rail row" is the world
        /// curve rather than no railway at all, and <paramref name="usedWorldFallback"/> is what lets the caller say
        /// which it got. </summary>
        public bool TryGetRail(int orderIndex, string continentKey, out SettlementRailFitRow row,
                               out bool usedWorldFallback)
        {
            usedWorldFallback = false;
            row = GetRail(orderIndex, continentKey);
            if (row != null) { return true; }
            if (continentKey == WorldKey) { return false; }
            row = GetRail(orderIndex, WorldKey);
            usedWorldFallback = row != null;
            return row != null;
        }

        /// <summary> B-27 / D-9. The UNDERGROUND row for one order in one continent bucket, or null when the table holds
        /// none. A third lookup for the reason <see cref="GetRail"/> is a second one: a caller siting a metro wants the
        /// metro row, and a table can answer the surface-rail question while holding no metro rows at all. </summary>
        public SettlementRailFitRow GetUnderground(int orderIndex, string continentKey)
        {
            for (int i = 0; i < undergroundRows.Count; i++)
            {
                if (undergroundRows[i] == null || undergroundRows[i].orderIndex != orderIndex) { continue; }
                if (undergroundRows[i].continentKey == continentKey) { return undergroundRows[i]; }
            }
            return null;
        }

        /// <summary> The metro row for that order and bucket, FALLING BACK to the world row when the bucket has none, for
        /// the reason <see cref="TryGetRail"/> does: asked at siting time about one settlement, the honest answer to "no
        /// European metro row" is the world curve rather than no metro at all, and <paramref name="usedWorldFallback"/>
        /// lets the caller say which it got. </summary>
        public bool TryGetUnderground(int orderIndex, string continentKey, out SettlementRailFitRow row,
                                      out bool usedWorldFallback)
        {
            usedWorldFallback = false;
            row = GetUnderground(orderIndex, continentKey);
            if (row != null) { return true; }
            if (continentKey == WorldKey) { return false; }
            row = GetUnderground(orderIndex, WorldKey);
            usedWorldFallback = row != null;
            return row != null;
        }

        /// <summary> B-27 / D-11. The LAND-USE row for one order in one continent bucket, or null when the table holds
        /// none. A fourth lookup for the reason <see cref="GetUnderground"/> is a third: a caller siting land use wants
        /// the composition row, and a table can answer the rail question while holding no land-use rows. </summary>
        public SettlementLandUseFitRow GetLandUse(int orderIndex, string continentKey)
        {
            for (int i = 0; i < landUseRows.Count; i++)
            {
                if (landUseRows[i] == null || landUseRows[i].orderIndex != orderIndex) { continue; }
                if (landUseRows[i].continentKey == continentKey) { return landUseRows[i]; }
            }
            return null;
        }

        /// <summary> The land-use row for that order and bucket, FALLING BACK to the world row when the bucket has none,
        /// for the reason <see cref="TryGetRail"/> does: asked at siting time about one settlement, the honest answer to
        /// "no European land-use row" is the world composition rather than no land use at all, and
        /// <paramref name="usedWorldFallback"/> lets the caller say which it got. </summary>
        public bool TryGetLandUse(int orderIndex, string continentKey, out SettlementLandUseFitRow row,
                                  out bool usedWorldFallback)
        {
            usedWorldFallback = false;
            row = GetLandUse(orderIndex, continentKey);
            if (row != null) { return true; }
            if (continentKey == WorldKey) { return false; }
            row = GetLandUse(orderIndex, WorldKey);
            usedWorldFallback = row != null;
            return row != null;
        }

        /// <summary> Every bucket this table holds rows for, <see cref="WorldKey"/> first and the rest in the order they
        /// appear. The window builds its filter from this rather than from a hard-coded list of continents, so a table
        /// fitted from a dump covering three continents offers three.
        ///
        /// It walks the rail rows as well as the population rows, so a bucket the railway file covered and the population
        /// file did not is still offered. That asymmetry is real: <c>allCountries.txt</c> is the whole planet and
        /// <c>cities1000</c> is a subset of it, so the railway pass can reach a bucket the population pass never
        /// saw. </summary>
        public IList<string> ContinentKeys()
        {
            List<string> keys = new List<string>();
            keys.Add(WorldKey);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || string.IsNullOrEmpty(rows[i].continentKey)) { continue; }
                if (!keys.Contains(rows[i].continentKey)) { keys.Add(rows[i].continentKey); }
            }
            for (int i = 0; i < railRows.Count; i++)
            {
                if (railRows[i] == null || string.IsNullOrEmpty(railRows[i].continentKey)) { continue; }
                if (!keys.Contains(railRows[i].continentKey)) { keys.Add(railRows[i].continentKey); }
            }
            for (int i = 0; i < undergroundRows.Count; i++)
            {
                if (undergroundRows[i] == null || string.IsNullOrEmpty(undergroundRows[i].continentKey)) { continue; }
                if (!keys.Contains(undergroundRows[i].continentKey)) { keys.Add(undergroundRows[i].continentKey); }
            }
            return keys;
        }

        /// <summary> Whether the table is usable, with a report either way. It requires the four world rows, because a
        /// table missing one order cannot answer for that order anywhere, and it requires every row it does hold to be
        /// shaped like a sorted sample -- a continent bucket whose quantiles fall outside its own observed range is named
        /// here rather than left to be discovered by whatever reads it.
        ///
        /// The record-count rule is asked only of a table that claims a fit. The shipped placeholder table was derived
        /// from the order catalogue and never counted a place, so holding it to a twenty-record minimum would fail the
        /// asset this package ships for reasons that do not apply to it; <see cref="ApplyTo"/> is where the count matters,
        /// and it refuses a placeholder table outright. </summary>
        public bool Validate(out string report)
        {
            List<string> problems = new List<string>();
            for (int index = SettlementOrderCatalog.CapitalOrder; index <= SettlementOrderCatalog.VillageOrder; index++)
            {
                if (Get(index, WorldKey) == null)
                {
                    problems.Add("no world row for order " + index + ", so this table cannot answer for that order at"
                                 + " all");
                }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null) { problems.Add("the row at position " + i + " is empty"); continue; }
                string reason;
                bool rowHolds = fittedFromRealDump
                    ? rows[i].IsUsable(out reason)
                    : rows[i].IsShapeConsistent(out reason);
                if (!rowHolds) { problems.Add(reason); }
            }
            for (int i = 0; i < railRows.Count; i++)
            {
                if (railRows[i] == null) { problems.Add("the rail row at position " + i + " is empty"); continue; }
                string railReason;
                bool railHolds = railFittedFromRealDump
                    ? railRows[i].IsUsable(out railReason)
                    : railRows[i].IsShapeConsistent(out railReason);
                if (!railHolds) { problems.Add(railReason); }
            }
            for (int i = 0; i < undergroundRows.Count; i++)
            {
                if (undergroundRows[i] == null)
                {
                    problems.Add("the underground row at position " + i + " is empty");
                    continue;
                }
                string undergroundReason;
                bool undergroundHolds = undergroundFittedFromRealDump
                    ? undergroundRows[i].IsUsable(out undergroundReason)
                    : undergroundRows[i].IsShapeConsistent(out undergroundReason);
                if (!undergroundHolds) { problems.Add(undergroundReason); }
            }
            if (problems.Count > 0)
            {
                report = "this settlement fit table cannot be used: " + string.Join("; ", problems.ToArray()) + ".";
                return false;
            }
            report = rows.Count + " rows across " + ContinentKeys().Count + " buckets, " + recordsUsed + " records used"
                     + " of " + recordsRead + " read from " + sourceName
                     + (fittedFromRealDump ? " (" + dumpFileName + ", fitted " + fittedOnUtc + ")"
                         : " -- AUTHORED PLACEHOLDERS, not fitted from a dump") + ". "
                     + (railRows.Count == 0
                         ? "No rail rows, so railway presence is not answered by this table."
                         : railRows.Count + " rail rows, " + railStationsRead + " stations read"
                           + (railFittedFromRealDump ? " from " + railDumpFileName
                               : " -- AUTHORED PLACEHOLDERS, not fitted from a dump")) + ". "
                     + (undergroundRows.Count == 0
                         ? "No underground rows, so metro presence is not answered by this table."
                         : undergroundRows.Count + " underground rows, " + undergroundStationsRead + " stations read"
                           + (undergroundFittedFromRealDump ? " from " + undergroundDumpFileName
                               : " -- AUTHORED PLACEHOLDERS, not fitted from a dump")) + ".";
            return true;
        }

        /// <summary> Writes this table's figures into an order catalogue for one continent bucket, or refuses and changes
        /// nothing. This is the ONLY path by which a fit reaches the catalogue, and it is here rather than in the window
        /// so that a gate can perform it without opening one.
        ///
        /// THE HARD PART IS THAT REAL DISTRIBUTIONS OVERLAP AND A BAND LADDER CANNOT. Half the world's second-order
        /// administrative seats are smaller than the largest ordinary populated place, so the fitted quantiles of two
        /// adjacent orders cross. A ladder of bands has to tile the number line -- every population belongs to exactly
        /// one order -- so the boundary between two orders is taken as the HIGHER of the lower order's 95th percentile
        /// and the higher order's 5th, and the report says by how much the raw distributions overlapped. Choosing the
        /// higher of the two is what keeps the bands from overlapping; reporting the overlap is what stops that choice
        /// from being invisible.
        ///
        /// IT REFUSES RATHER THAN HALF-WRITING. The catalogue's own <see cref="SettlementOrderCatalog.Validate"/> is run
        /// after the write, and if the fitted numbers break the ladder every field is put back exactly as it was. An
        /// author who imports a dump covering one country should get a refusal naming the problem, not a catalogue that
        /// no longer validates. </summary>
        public bool ApplyTo(SettlementOrderCatalog catalog, string continentKey, out string report)
        {
            if (catalog == null)
            {
                report = "no order catalogue was supplied, so there is nothing to write these figures into.";
                return false;
            }
            if (!fittedFromRealDump)
            {
                report = "this fit table holds authored placeholders rather than a fit from a real dump, and writing"
                         + " them into a catalogue would mark that catalogue as fitted from data when nothing was"
                         + " measured. Import a dump first.";
                return false;
            }
            SettlementFitRow[] fitted = new SettlementFitRow[5];
            SettlementOrderClass[] target = new SettlementOrderClass[5];
            for (int index = SettlementOrderCatalog.CapitalOrder; index <= SettlementOrderCatalog.VillageOrder; index++)
            {
                fitted[index] = Get(index, continentKey);
                target[index] = catalog.Get(index);
                if (fitted[index] == null)
                {
                    report = "this table holds no row for order " + index + " in " + continentKey + ", so that order"
                             + " would keep its authored numbers while the others were replaced -- a half-fitted ladder"
                             + " is not a ladder. Apply the " + WorldKey + " rows instead, or fit a dump that covers"
                             + " this bucket.";
                    return false;
                }
                if (target[index] == null)
                {
                    report = "the order catalogue holds no order with the index " + index + ", so there is nothing to"
                             + " write order " + index + "'s fitted figures into.";
                    return false;
                }
                string reason;
                if (!fitted[index].IsUsable(out reason)) { report = reason + "."; return false; }
            }
            // Every field this method touches, kept so the catalogue can be put back byte for byte if the fitted ladder
            // fails its own validation. Unity cannot roll back a change to a serialised object for us.
            int[] wasPopulationMin = new int[5];
            int[] wasPopulationMax = new int[5];
            float[] wasSpacingMin = new float[5];
            float[] wasSpacingMax = new float[5];
            float[] wasRankSize = new float[5];
            bool wasFitted = catalog.fittedFromData;
            string wasProvenance = catalog.fitProvenance;
            for (int index = SettlementOrderCatalog.CapitalOrder; index <= SettlementOrderCatalog.VillageOrder; index++)
            {
                wasPopulationMin[index] = target[index].populationMin;
                wasPopulationMax[index] = target[index].populationMax;
                wasSpacingMin[index] = target[index].spacingMinMetres;
                wasSpacingMax[index] = target[index].spacingMaxMetres;
                wasRankSize[index] = target[index].rankSizeExponent;
            }

            int[] boundary = new int[5];
            List<string> overlaps = new List<string>();
            for (int index = SettlementOrderCatalog.CapitalOrder; index < SettlementOrderCatalog.VillageOrder; index++)
            {
                int higherFifth = fitted[index].populationP05;
                int lowerNinetyFifth = fitted[index + 1].populationP95;
                boundary[index] = higherFifth > lowerNinetyFifth ? higherFifth : lowerNinetyFifth;
                if (lowerNinetyFifth > higherFifth)
                {
                    overlaps.Add("orders " + index + " and " + (index + 1) + " overlap by "
                                 + (lowerNinetyFifth - higherFifth) + " people where the fit measured them, and the"
                                 + " boundary was taken at " + boundary[index]);
                }
            }
            for (int index = SettlementOrderCatalog.CapitalOrder; index <= SettlementOrderCatalog.VillageOrder; index++)
            {
                // The top of the ladder has no order above it, so its ceiling is the largest place observed rather than
                // a boundary; the bottom has none below it, so its floor stays authored -- cities1000 holds places of a
                // thousand people and up and cannot see a hamlet of two hundred.
                target[index].populationMax = index > SettlementOrderCatalog.CapitalOrder
                    ? boundary[index - 1] : fitted[index].populationObservedMax;
                if (index < SettlementOrderCatalog.VillageOrder)
                {
                    target[index].populationMin = boundary[index];
                }
                target[index].spacingMinMetres = fitted[index].spacingP25Metres;
                target[index].spacingMaxMetres = fitted[index].spacingP75Metres;
                target[index].rankSizeExponent = fitted[index].rankSizeExponent;
            }
            catalog.fittedFromData = true;
            catalog.fitProvenance = sourceName + " (" + sourceLicence + "), file " + dumpFileName + ", fitted "
                                    + fittedOnUtc + " from " + recordsUsed + " records of " + recordsRead
                                    + " read, bucket " + continentKey + ". Allometric exponents NOT fitted: "
                                    + allometryNote;

            string ladderReport;
            if (!catalog.Validate(out ladderReport))
            {
                for (int index = SettlementOrderCatalog.CapitalOrder;
                     index <= SettlementOrderCatalog.VillageOrder; index++)
                {
                    target[index].populationMin = wasPopulationMin[index];
                    target[index].populationMax = wasPopulationMax[index];
                    target[index].spacingMinMetres = wasSpacingMin[index];
                    target[index].spacingMaxMetres = wasSpacingMax[index];
                    target[index].rankSizeExponent = wasRankSize[index];
                }
                catalog.fittedFromData = wasFitted;
                catalog.fitProvenance = wasProvenance;
                report = "the fitted figures for " + continentKey + " do not make a usable ladder, so the catalogue was"
                         + " put back exactly as it was and nothing changed. " + ladderReport;
                return false;
            }

            string bands = "";
            for (int index = SettlementOrderCatalog.CapitalOrder; index <= SettlementOrderCatalog.VillageOrder; index++)
            {
                bands += " Order " + index + " (" + target[index].displayName + "): " + target[index].populationMin
                         + " to " + target[index].populationMax + " people, spacing "
                         + target[index].spacingMinMetres.ToString("0") + " to "
                         + target[index].spacingMaxMetres.ToString("0") + " m measured, replacing "
                         + wasSpacingMin[index].ToString("0") + " to " + wasSpacingMax[index].ToString("0")
                         + " m cited; rank-size exponent " + target[index].rankSizeExponent.ToString("0.###") + ".";
            }
            report = "wrote the " + continentKey + " fit into " + catalog.name + " from " + recordsUsed + " real places."
                     + bands + " The spacing figures the citation gave are replaced by measurements and are named above"
                     + " so the citation is not lost silently; the allometric exponents are untouched, because this"
                     + " source cannot fit them."
                     + (overlaps.Count > 0 ? " Raw distributions overlapped: " + string.Join("; ", overlaps.ToArray())
                         + "." : " The fitted quantiles did not overlap between adjacent orders.");
            return true;
        }

        /// <summary> The table that SHIPS: one world row per order, derived from the order catalogue's own authored
        /// bands, marked as not fitted from a dump.
        ///
        /// It is built FROM the catalogue rather than typed out, so the shipped magnitudes exist in exactly one place --
        /// <c>SettlementOrderCatalog.ResetToDefaults</c> -- and this table cannot drift away from them. What it adds is a
        /// SHAPE inside each band, since a band alone says nothing about where within it a place is likely to fall.
        ///
        /// THE SHAPE IS AN ASSUMPTION AND IS LABELLED ONE: the quantiles are spaced geometrically, which is to say the
        /// placeholder treats population as log-uniform inside the band. Geometric rather than arithmetic because the
        /// bands span orders of magnitude and a settlement distribution is heavy-tailed, so an arithmetic midpoint of
        /// 250,000 and 20,000,000 would call ten million a typical big city. It is not a measurement of anything, which
        /// is why <see cref="fittedFromRealDump"/> stays false and <see cref="ApplyTo"/> refuses to write this table into
        /// a catalogue: it came from that catalogue, and feeding it back would launder authored numbers into fitted
        /// ones. </summary>
        public static SettlementFitTable CreateAuthored(SettlementOrderCatalog catalog)
        {
            SettlementFitTable table = ScriptableObject.CreateInstance<SettlementFitTable>();
            table.fittedFromRealDump = false;
            table.allometryFitted = false;
            table.recordsRead = 0;
            table.recordsUsed = 0;
            if (catalog == null) { return table; }
            table.railFittedFromRealDump = false;
            table.undergroundFittedFromRealDump = false;
            table.landUseFittedFromRealDump = false;
            for (int index = SettlementOrderCatalog.CapitalOrder;
                 index <= SettlementOrderCatalog.VillageOrder; index++)
            {
                SettlementOrderClass order = catalog.Get(index);
                if (order == null) { continue; }
                table.rows.Add(AuthoredRow(order));
                table.railRows.Add(AuthoredRailRow(order.orderIndex));
                table.undergroundRows.Add(AuthoredUndergroundRow(order.orderIndex));
                table.landUseRows.Add(AuthoredLandUseRow(order.orderIndex));
            }
            return table;
        }

        /// <summary> One authored RAIL row per order, so the shipped table answers the railway question before anybody
        /// imports a dump. The figures are the defaults on <see cref="SettlementRailFitRow"/> itself, shifted per order
        /// only where the shift is the whole point: a capital's presence curve is not a village's.
        ///
        /// What moves is the logistic INTERCEPT, not its slope. The slope is how sharply the odds of a railway rise with
        /// population, and there is no reason to author that differently per order; the intercept is where the curve sits,
        /// and the ladder's own orders are the reason it should differ -- an administrative capital of 40,000 is served
        /// where an ordinary town of 40,000 often is not, because the railway was built to reach the seat of government.
        /// So the higher orders get a raised intercept and the village gets a lowered one, and every one of these numbers
        /// is an AUTHORED PLACEHOLDER that <see cref="railFittedFromRealDump"/> reports as such. </summary>
        private static SettlementRailFitRow AuthoredRailRow(int orderIndex)
        {
            SettlementRailFitRow row = new SettlementRailFitRow();
            row.orderIndex = orderIndex;
            row.continentKey = WorldKey;
            row.recordCount = 0;
            row.placesWithStation = 0;
            if (orderIndex == SettlementOrderCatalog.CapitalOrder) { row.logisticIntercept = -4.2f; }
            else if (orderIndex == SettlementOrderCatalog.CityOrder) { row.logisticIntercept = -5.0f; }
            else if (orderIndex == SettlementOrderCatalog.TownOrder) { row.logisticIntercept = -6.0f; }
            else { row.logisticIntercept = -7.4f; }
            row.logisticFitQuality = 0f;
            row.stationCountFitQuality = 0f;
            return row;
        }

        /// <summary> B-27 / D-9. One authored UNDERGROUND row per order, so the shipped table answers the metro question
        /// before anybody imports a metro dump -- the same service <see cref="AuthoredRailRow"/> does for surface rail,
        /// on the same <see cref="SettlementRailFitRow"/> type, because a metro's presence, station count and spacing are
        /// the same three fitted shapes. Only the NUMBERS differ, and the differences are the whole point:
        ///
        /// PRESENCE sits far lower. A metro is a large-city and capital feature: the intercepts here put a two-million
        /// capital near three-in-five, a two-million ordinary city near even, a town near one-in-a-hundred and a village
        /// at nothing -- against the surface curve, where a capital of a thousand already has rail half the time. As with
        /// rail only the logistic INTERCEPT moves per order and the slope is left at its default, because a steeper rise
        /// per order would be an authored claim about the SHAPE of the curve that a placeholder has no measurement to
        /// make. These are the four populations <see cref="SettlementRailFitRow.Describe"/> prints, so an author can read
        /// the curve back and disagree with it.
        ///
        /// STATIONS sit closer and are more numerous. Metro interstation spacing runs a few hundred metres to a couple of
        /// kilometres, not the kilometres-to-tens of a mainline, so the quantiles are 600 / 1,000 / 1,800 m; and a metro
        /// is a dense in-city network rather than a handful of mainline calls, so the count reference and its exponent are
        /// raised -- four stations at a hundred thousand, growing as the square root of population, which gives a
        /// two-million city about eighteen. Every figure is an AUTHORED PLACEHOLDER that
        /// <see cref="undergroundFittedFromRealDump"/> reports as such, replaced field for field when a real metro dump is
        /// fitted. </summary>
        private static SettlementRailFitRow AuthoredUndergroundRow(int orderIndex)
        {
            SettlementRailFitRow row = new SettlementRailFitRow();
            row.orderIndex = orderIndex;
            row.continentKey = WorldKey;
            row.recordCount = 0;
            row.placesWithStation = 0;
            if (orderIndex == SettlementOrderCatalog.CapitalOrder) { row.logisticIntercept = -8.5f; }
            else if (orderIndex == SettlementOrderCatalog.CityOrder) { row.logisticIntercept = -9.0f; }
            else if (orderIndex == SettlementOrderCatalog.TownOrder) { row.logisticIntercept = -10.5f; }
            else { row.logisticIntercept = -12.5f; }
            row.logisticFitQuality = 0f;
            row.stationCountExponent = 0.5f;
            row.stationCountReferencePopulation = 100000;
            row.stationCountAtReference = 4f;
            row.stationCountFitQuality = 0f;
            row.stationSpacingP25Metres = 600f;
            row.stationSpacingP50Metres = 1000f;
            row.stationSpacingP75Metres = 1800f;
            row.stationSearchRadiusMetres = 4000f;
            return row;
        }

        /// <summary> B-27 / D-11. One authored LAND-USE row per order, so the shipped table answers "how much of each
        /// use" before anybody imports a land-use dump -- the same service <see cref="AuthoredRailRow"/> does for rail.
        /// The shares are AUTHORED PLACEHOLDERS that <see cref="landUseFittedFromRealDump"/> reports as such, and they
        /// move per order because the composition of a capital is not a village's:
        ///
        /// RESIDENTIAL rises as the order falls -- a village is almost all homes, a capital spreads its ground across
        /// more uses. COMMERCIAL and INDUSTRIAL concentrate in the higher orders, where the offices and the sheds are.
        /// MARKET stays small everywhere, because a market is a focused open ground rather than a spread of buildings,
        /// and sits a touch higher in the smaller places where the periodic market is the main commerce. Each row sums
        /// to one; a real fit replaces all four field for field. </summary>
        private static SettlementLandUseFitRow AuthoredLandUseRow(int orderIndex)
        {
            SettlementLandUseFitRow row = new SettlementLandUseFitRow();
            row.orderIndex = orderIndex;
            row.continentKey = WorldKey;
            row.recordCount = 0;
            if (orderIndex == SettlementOrderCatalog.CapitalOrder)
            {
                row.residentialShare = 0.55f; row.commercialShare = 0.20f;
                row.marketShare = 0.04f; row.industrialShare = 0.21f;
            }
            else if (orderIndex == SettlementOrderCatalog.CityOrder)
            {
                row.residentialShare = 0.60f; row.commercialShare = 0.16f;
                row.marketShare = 0.04f; row.industrialShare = 0.20f;
            }
            else if (orderIndex == SettlementOrderCatalog.TownOrder)
            {
                row.residentialShare = 0.68f; row.commercialShare = 0.12f;
                row.marketShare = 0.05f; row.industrialShare = 0.15f;
            }
            else
            {
                row.residentialShare = 0.82f; row.commercialShare = 0.06f;
                row.marketShare = 0.05f; row.industrialShare = 0.07f;
            }
            return row;
        }

        /// <summary> One authored row from one authored order, by the geometric spacing
        /// <see cref="CreateAuthored"/> documents. </summary>
        private static SettlementFitRow AuthoredRow(SettlementOrderClass order)
        {
            float populationMid = Mathf.Sqrt((float)order.populationMin * order.populationMax);
            SettlementFitRow row = new SettlementFitRow();
            row.orderIndex = order.orderIndex;
            row.continentKey = WorldKey;
            row.recordCount = 0;
            row.populationP05 = order.populationMin;
            row.populationP25 = Mathf.RoundToInt(Mathf.Sqrt(order.populationMin * populationMid));
            row.populationP50 = Mathf.RoundToInt(populationMid);
            row.populationP75 = Mathf.RoundToInt(Mathf.Sqrt(populationMid * order.populationMax));
            row.populationP95 = order.populationMax;
            row.populationObservedMin = order.populationMin;
            row.populationObservedMax = order.populationMax;
            row.spacingP25Metres = order.spacingMinMetres;
            row.spacingP50Metres = Mathf.Sqrt(order.spacingMinMetres * order.spacingMaxMetres);
            row.spacingP75Metres = order.spacingMaxMetres;
            row.rankSizeExponent = order.rankSizeExponent;
            row.rankSizeR2 = 0f;
            return row;
        }
    }
}
