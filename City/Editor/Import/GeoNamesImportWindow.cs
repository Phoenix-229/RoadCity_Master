#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> Fits a <see cref="SettlementFitTable"/> from a GeoNames <c>cities1000</c> dump, so the settlement
    /// order ladder can be measured from real places instead of asserted.
    ///
    /// THE DUMP IS NOT SHIPPED. GeoNames is CC-BY 4.0 and redistributable, but a 130,000-line text file inside a Unity
    /// package is 13 MB every author pays for and almost none of them read, so this window asks for the path to a copy
    /// the author downloaded themselves. What ships is the derived table -- five quantiles, three spacings and one
    /// exponent per order -- and the attribution, which is in THIRD-PARTY-NOTICES.md.
    ///
    /// WHAT IT CAN AND CANNOT FIT. The dump carries population, position, feature code, elevation and timezone. So the
    /// population quantiles are counted, the nearest-neighbour spacings are measured, and the rank-size exponent is
    /// fitted. It carries no built-up area and no street length, so the two allometric exponents in the order catalogue
    /// are NOT touched here and stay authored with their form cited -- see <c>SettlementFitTable.allometryNote</c>.
    ///
    /// It has its own menu row by the RM-108 test: before it runs there need be no settlement fit table asset in the
    /// project at all, so there is nothing an author could select to reach it from an inspector. </summary>
    public sealed class GeoNamesImportWindow : EditorWindow
    {
        // The cities1000 layout, documented at https://download.geonames.org/export/dump/readme.txt. Named rather than
        // spelled inline so a column that moves is one edit and not a hunt.
        private const int ColumnCount = 19;
        private const int LatitudeColumn = 4;
        private const int LongitudeColumn = 5;
        private const int FeatureCodeColumn = 7;
        private const int PopulationColumn = 14;
        private const int TimeZoneColumn = 17;

        // The nearest-neighbour search grid. One degree is about 111 km north-south, which comfortably contains the
        // spacing of a village ladder and is widened ring by ring for the sparse orders. MaxRing bounds the work on a
        // place with no neighbour at all -- an island capital -- rather than letting it scan the world.
        private const double CellDegrees = 1.0;
        private const int MaxRing = 12;

        /// <summary> One dump row, narrowed to the five columns that are read. <c>orderIndex</c> is zero while a
        /// <c>PPL</c>/<c>PPLX</c> row is still waiting for the median split that decides town from village. </summary>
        private struct Place
        {
            public double latitudeDeg;
            public double longitudeDeg;
            public int population;
            public int orderIndex;
            public string continentKey;
        }

        private string dumpPath = "";
        private SettlementFitTable targetTable;
        private SettlementOrderCatalog orderCatalog;
        private bool splitByContinent = true;
        private string report = "";
        private Vector2 reportScroll;
        private readonly List<SettlementFitRow> fittedRows = new List<SettlementFitRow>();
        private int recordsRead;
        private int recordsUsed;
        private string dumpFileName = "";
        private string fittedOnUtc = "";

        // B-27 / D-6. The railway half. Kept in its own fields rather than folded into the ones above, because the two
        // fits read two different files and a table can hold one and not the other -- the same separation
        // SettlementFitTable draws between its rows and its railRows.
        private string railDumpPath = "";
        private readonly List<SettlementRailFitRow> railFittedRows = new List<SettlementRailFitRow>();
        private int railStationsRead;
        private string railDumpFileName = "";

        // The places the population pass parsed, kept so the railway pass can ask about them instead of reading
        // cities1000 a second time. The railway fit is a question ABOUT settlements -- does a place of this size have a
        // station near it -- so it needs both files, and re-reading the smaller one would only risk the two passes
        // disagreeing about which places exist.
        private readonly List<Place> fittedPlaces = new List<Place>();


        /// <summary> Opens the window. </summary>
        [MenuItem("Window/Road City Master/Fit settlement orders from GeoNames", false, 2)]
        public static void ShowWindow()
        {
            GeoNamesImportWindow window = GetWindow<GeoNamesImportWindow>(false, "GeoNames fit", true);
            window.minSize = new Vector2(560f, 380f);
            window.Show();
        }


        private void OnGUI()
        {
            EditorGUILayout.LabelField("GeoNames cities1000", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Download cities1000.zip from " + SettlementFitTable.SourceUrlDefault + ", extract"
                + " it, and point at cities1000.txt. The dump stays where you put it -- only the fitted table is saved"
                + " into the project, and the attribution is already in THIRD-PARTY-NOTICES.md.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            dumpPath = EditorGUILayout.TextField("Dump file", dumpPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70f)))
            {
                string picked = EditorUtility.OpenFilePanel("Pick the extracted cities1000.txt", "", "txt");
                if (!string.IsNullOrEmpty(picked)) { dumpPath = picked; }
            }
            EditorGUILayout.EndHorizontal();
            splitByContinent = EditorGUILayout.ToggleLeft("Fit a row per continent bucket as well as the world row",
                splitByContinent);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(dumpPath)))
            {
                if (GUILayout.Button("Read the dump and fit the four orders"))
                {
                    string fitReport;
                    ReadAndFit(out fitReport);
                    report = fitReport;
                    Debug.Log(fitReport);
                    GUIUtility.keyboardControl = 0;
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fitted rows: " + fittedRows.Count + "    records used: "
                + recordsUsed.ToString("n0") + " of " + recordsRead.ToString("n0") + " read");

            // B-27 / D-6. The railway half, in its own block below the population half rather than interleaved with it,
            // because it is a SECOND FILE and the order matters: the presence curve is fitted by joining stations to the
            // places the pass above held, so it cannot run first.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GeoNames allCountries (railways)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("cities1000 carries no railway of any kind, so railway presence is fitted from a"
                + " second file: allCountries.zip from the same page, extracted, pointed at below. Only the rows whose"
                + " feature code is RSTN (station) or RSTP (stop) are read -- the file is around 1.5 GB and this pass"
                + " keeps two columns of the rows it wants and discards the rest as it streams. Fit the population dump"
                + " first: the presence curve asks, of each place THAT dump held, whether a station stands near it.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            railDumpPath = EditorGUILayout.TextField("Railway file", railDumpPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70f)))
            {
                string pickedRail = EditorUtility.OpenFilePanel("Pick the extracted allCountries.txt", "", "txt");
                if (!string.IsNullOrEmpty(pickedRail)) { railDumpPath = pickedRail; }
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(railDumpPath) || fittedPlaces.Count == 0))
            {
                if (GUILayout.Button(fittedPlaces.Count == 0
                        ? "Read the dump and fit the four orders first"
                        : "Read the railway file and fit the presence curves"))
                {
                    string railReport;
                    ReadAndFitRail(out railReport);
                    report = railReport;
                    Debug.Log(railReport);
                    GUIUtility.keyboardControl = 0;
                }
            }
            EditorGUILayout.LabelField("Rail rows: " + railFittedRows.Count + "    stations read: "
                + railStationsRead.ToString("n0"));

            EditorGUILayout.Space();
            targetTable = (SettlementFitTable)EditorGUILayout.ObjectField("Fit table", targetTable,
                typeof(SettlementFitTable), false);
            using (new EditorGUI.DisabledScope(fittedRows.Count == 0))
            {
                if (GUILayout.Button(targetTable == null
                        ? "Save the fitted table as a new asset"
                        : "Write the fitted rows into " + targetTable.name))
                {
                    report = WriteFittedAsset();
                }
            }
            orderCatalog = (SettlementOrderCatalog)EditorGUILayout.ObjectField("Order catalogue", orderCatalog,
                typeof(SettlementOrderCatalog), false);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(orderCatalog == null || targetTable == null))
            {
                if (GUILayout.Button("Apply the world rows to the catalogue"))
                {
                    string applyReport;
                    targetTable.ApplyTo(orderCatalog, SettlementFitTable.WorldKey, out applyReport);
                    EditorUtility.SetDirty(orderCatalog);
                    AssetDatabase.SaveAssets();
                    report = applyReport;
                }
            }
            using (new EditorGUI.DisabledScope(orderCatalog == null))
            {
                if (GUILayout.Button("Create the shipped placeholder table"))
                {
                    report = WritePlaceholderAsset();
                }
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(report))
            {
                EditorGUILayout.Space();
                reportScroll = EditorGUILayout.BeginScrollView(reportScroll);
                EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }
        /// <summary> Reads the dump once, assigns every place an order, and fits one row per bucket. Reports a named
        /// reason instead of leaving a half-filled table behind.
        ///
        /// The read is a single streaming pass -- the file is around 130,000 lines and there is no reason to hold it
        /// twice -- but the order of a <c>PPL</c> row cannot be settled during that pass, because town versus village is
        /// the median of the <c>PPL</c> population distribution and the median is not known until the last line has been
        /// read. So the pass collects, and the split follows it. </summary>
        private bool ReadAndFit(out string reportOut)
        {
            fittedRows.Clear();
            fittedPlaces.Clear();
            // The railway fit is asked about THESE places, so a re-read of the population dump invalidates it. Cleared
            // rather than left standing, because a rail row fitted against a previous dump written into a table fitted
            // from this one would be two files' answers wearing one provenance stamp.
            railFittedRows.Clear();
            railStationsRead = 0;
            recordsRead = 0;
            recordsUsed = 0;
            if (!File.Exists(dumpPath))
            {
                reportOut = "Road & City Master: there is no file at " + dumpPath + ", so nothing was fitted.";
                return false;
            }
            List<Place> places = new List<Place>(150000);
            int malformed = 0;
            int notSettlements = 0;
            try
            {
                using (StreamReader reader = new StreamReader(dumpPath))
                {
                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        recordsRead++;
                        Place place;
                        int outcome = TryReadPlace(line, out place);
                        if (outcome == ReadMalformed) { malformed++; }
                        else if (outcome == ReadNotSettlement) { notSettlements++; }
                        else { places.Add(place); }
                        line = reader.ReadLine();
                    }
                }
            }
            catch (IOException error)
            {
                reportOut = "Road & City Master: the dump at " + dumpPath + " could not be read (" + error.Message
                            + "), so nothing was fitted.";
                return false;
            }
            if (places.Count == 0)
            {
                reportOut = "Road & City Master: " + recordsRead.ToString("n0") + " lines were read from "
                            + Path.GetFileName(dumpPath) + " and not one of them parsed as a populated place. This"
                            + " window expects the tab-separated cities1000.txt exactly as GeoNames publishes it --"
                            + " extracted from cities1000.zip, not opened and re-saved by a spreadsheet.";
                return false;
            }
            SplitPplFamilyAtMedian(places);
            // Kept for the railway pass, AFTER the median split, so the two fits agree about which places are towns.
            fittedPlaces.Clear();
            fittedPlaces.AddRange(places);
            dumpFileName = Path.GetFileName(dumpPath);
            fittedOnUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            List<string> buckets = new List<string>();
            buckets.Add(SettlementFitTable.WorldKey);
            if (splitByContinent)
            {
                for (int i = 0; i < places.Count; i++)
                {
                    if (!buckets.Contains(places[i].continentKey)) { buckets.Add(places[i].continentKey); }
                }
            }
            List<string> notes = new List<string>();
            for (int b = 0; b < buckets.Count; b++)
            {
                for (int order = SettlementOrderCatalog.CapitalOrder;
                     order <= SettlementOrderCatalog.VillageOrder; order++)
                {
                    List<Place> bucket = Gather(places, order, buckets[b]);
                    if (bucket.Count < SettlementFitRow.MinimumRecordCount)
                    {
                        notes.Add(buckets[b] + " order " + order + " held " + bucket.Count + " places, fewer than the "
                                  + SettlementFitRow.MinimumRecordCount + " a percentile can be read from, so no row"
                                  + " was fitted for it");
                        continue;
                    }
                    fittedRows.Add(FitRow(bucket, order, buckets[b]));
                    // Counted on the world pass only. A continent row re-reads the same places, so adding both would
                    // report more records used than the file holds.
                    if (buckets[b] == SettlementFitTable.WorldKey) { recordsUsed += bucket.Count; }
                }
            }
            reportOut = BuildFitReport(malformed, notSettlements, notes);
            return fittedRows.Count > 0;
        }
        /// <summary> B-27 / D-6. Reads the railway file and fits one rail row per bucket per order, or reports why not.
        ///
        /// THIS IS A SECOND FILE AND THERE IS NO WAY AROUND IT. <c>cities1000.txt</c> carries population, position,
        /// feature code, elevation and time zone. It carries no railway of any kind, so a rail row fitted from it would
        /// be an invented number wearing a citation. The station points live in <c>allCountries.txt</c> -- the same
        /// export, the whole gazetteer -- under feature codes <c>RSTN</c> (railway station) and <c>RSTP</c> (railway
        /// stop), and that file is about 1.5 GB extracted. So this pass keeps only the two columns it needs from the rows
        /// it wants and discards every other line as it goes; it never holds the file.
        ///
        /// WHAT IS FITTED IS A JOIN, NOT A COUNT. The question is not how many stations exist but whether a settlement of
        /// a given size has one, so each place from the population pass is asked whether a station lies within its row's
        /// search radius. That makes each place one observation -- population, and served or not -- which is exactly the
        /// sample a logistic is fitted from.
        ///
        /// It requires the population pass to have run first, because it has nothing to join the stations TO
        /// otherwise. </summary>
        private bool ReadAndFitRail(out string reportOut)
        {
            railFittedRows.Clear();
            railStationsRead = 0;
            if (fittedPlaces.Count == 0)
            {
                reportOut = "Road & City Master: read the population dump first. The railway fit asks, of each place that"
                            + " dump held, whether a station stands near it -- with no places there is nothing to ask"
                            + " about.";
                return false;
            }
            if (!File.Exists(railDumpPath))
            {
                reportOut = "Road & City Master: there is no file at " + railDumpPath + ", so no railway rows were"
                            + " fitted.";
                return false;
            }
            List<GeoCoordinate> stations = new List<GeoCoordinate>(100000);
            int linesRead = 0;
            try
            {
                using (StreamReader reader = new StreamReader(railDumpPath))
                {
                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        linesRead++;
                        GeoCoordinate station;
                        if (TryReadStation(line, out station)) { stations.Add(station); }
                        line = reader.ReadLine();
                    }
                }
            }
            catch (IOException error)
            {
                reportOut = "Road & City Master: the railway dump at " + railDumpPath + " could not be read ("
                            + error.Message + "), so no railway rows were fitted.";
                return false;
            }
            if (stations.Count == 0)
            {
                reportOut = "Road & City Master: " + linesRead.ToString("n0") + " lines were read from "
                            + Path.GetFileName(railDumpPath) + " and not one of them was a railway station. This pass"
                            + " wants allCountries.txt as GeoNames publishes it, and reads only the rows whose feature"
                            + " code is RSTN or RSTP -- cities1000.txt contains none of those.";
                return false;
            }
            railStationsRead = stations.Count;
            railDumpFileName = Path.GetFileName(railDumpPath);
            Dictionary<long, List<int>> stationGrid = BuildStationGrid(stations);
            List<string> notes = new List<string>();
            List<string> buckets = new List<string>();
            buckets.Add(SettlementFitTable.WorldKey);
            if (splitByContinent)
            {
                for (int i = 0; i < fittedPlaces.Count; i++)
                {
                    if (!buckets.Contains(fittedPlaces[i].continentKey)) { buckets.Add(fittedPlaces[i].continentKey); }
                }
            }
            for (int b = 0; b < buckets.Count; b++)
            {
                for (int order = SettlementOrderCatalog.CapitalOrder;
                     order <= SettlementOrderCatalog.VillageOrder; order++)
                {
                    List<Place> bucket = Gather(fittedPlaces, order, buckets[b]);
                    if (bucket.Count < SettlementRailFitRow.MinimumRecordCount)
                    {
                        notes.Add(buckets[b] + " order " + order + " held " + bucket.Count + " places, fewer than the "
                                  + SettlementRailFitRow.MinimumRecordCount + " a presence curve can be fitted from, so"
                                  + " no rail row was fitted for it");
                        continue;
                    }
                    string reason;
                    SettlementRailFitRow row = FitRailRow(bucket, stations, stationGrid, order, buckets[b], out reason);
                    if (row == null) { notes.Add(buckets[b] + " order " + order + ": " + reason); continue; }
                    railFittedRows.Add(row);
                }
            }
            reportOut = BuildRailFitReport(linesRead, notes);
            return railFittedRows.Count > 0;
        }


        /// <summary> One railway row from <c>allCountries.txt</c>, or false for every other line. It reads the two
        /// coordinate columns and the feature code and nothing else -- no name, no population, no admin codes -- because
        /// the only question asked of a station is where it is, and this runs over a million and a half lines.
        ///
        /// <c>allCountries.txt</c> and <c>cities1000.txt</c> share their layout exactly (both are the geoname table), so
        /// the same column constants serve. That is the reason this lives beside <see cref="TryReadPlace"/> rather than in
        /// a second reader with its own numbers to keep in step. </summary>
        private static bool TryReadStation(string line, out GeoCoordinate station)
        {
            station = new GeoCoordinate(0.0, 0.0);
            if (string.IsNullOrEmpty(line)) { return false; }
            string[] columns = line.Split('\t');
            if (columns.Length < ColumnCount) { return false; }
            string code = columns[FeatureCodeColumn];
            if (code != RailwayStationCode && code != RailwayStopCode) { return false; }
            double latitude, longitude;
            if (!double.TryParse(columns[LatitudeColumn], NumberStyles.Float, CultureInfo.InvariantCulture,
                                 out latitude)) { return false; }
            if (!double.TryParse(columns[LongitudeColumn], NumberStyles.Float, CultureInfo.InvariantCulture,
                                 out longitude)) { return false; }
            station = new GeoCoordinate(latitude, longitude);
            return true;
        }


        /// <summary> The station points indexed by the same one-degree cell grid the spacing fit uses, so asking whether a
        /// station stands within five kilometres of a place is a handful of cell lookups rather than a scan of a hundred
        /// thousand points for each of a hundred thousand places. </summary>
        private static Dictionary<long, List<int>> BuildStationGrid(List<GeoCoordinate> stations)
        {
            Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < stations.Count; i++)
            {
                long key = CellKeyOf(LatCellOf(stations[i].latitudeDeg), LonCellOf(stations[i].longitudeDeg));
                List<int> cell;
                if (!grid.TryGetValue(key, out cell))
                {
                    cell = new List<int>();
                    grid.Add(key, cell);
                }
                cell.Add(i);
            }
            return grid;
        }


        private const int ReadOk = 0;
        private const int ReadMalformed = 1;
        private const int ReadNotSettlement = 2;
        private const int PplFamilyOrder = -1;

        // The two GeoNames feature codes for a railway, documented at
        // https://www.geonames.org/export/codes.html. RSTN is a station; RSTP is a stop -- a halt with no building. Both
        // count as served, because a settlement whose train stops there has a railway whether or not it has a booking
        // hall, and excluding stops would systematically under-count the small orders where halts are the normal case.
        private const string RailwayStationCode = "RSTN";
        private const string RailwayStopCode = "RSTP";


        /// <summary> Reads one dump line into a place. Malformed means the line is not the published layout at all;
        /// not-a-settlement means it parsed and this fit has nothing to say about it -- a feature code outside the four
        /// orders, or a row carrying no population, which cannot sit in a population band. </summary>
        private static int TryReadPlace(string line, out Place place)
        {
            place = new Place();
            if (string.IsNullOrEmpty(line)) { return ReadMalformed; }
            string[] fields = line.Split('\t');
            if (fields.Length < ColumnCount) { return ReadMalformed; }
            int order = OrderForFeatureCode(fields[FeatureCodeColumn]);
            if (order == 0) { return ReadNotSettlement; }
            int population;
            if (!int.TryParse(fields[PopulationColumn], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out population) || population <= 0)
            {
                return ReadNotSettlement;
            }
            double latitude, longitude;
            if (!double.TryParse(fields[LatitudeColumn], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out latitude)
                || !double.TryParse(fields[LongitudeColumn], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out longitude))
            {
                return ReadMalformed;
            }
            place.latitudeDeg = latitude;
            place.longitudeDeg = longitude;
            place.population = population;
            place.orderIndex = order == PplFamilyOrder ? 0 : order;
            place.continentKey = SettlementFitTable.ContinentKeyFromTimeZone(fields[TimeZoneColumn]);
            return ReadOk;
        }
        /// <summary> The order a GeoNames feature code belongs to, fixed by the register's own table: <c>PPLC</c> and
        /// <c>PPLA</c> are order 1, <c>PPLA2</c> and <c>PPLA3</c> order 2, <c>PPLA4</c> and <c>PPLA5</c> order 3, and
        /// <c>PPL</c>/<c>PPLX</c> are split at their own median into town and village. Zero means this fit does not
        /// speak for the code.
        ///
        /// The unlisted codes are left out deliberately: <c>PPLL</c> a locality with no administration, <c>PPLF</c> a
        /// farm village, <c>PPLS</c> several places counted as one, <c>PPLR</c> a religious settlement, <c>PPLQ</c> and
        /// <c>PPLW</c> abandoned and destroyed, <c>PPLH</c> historical, <c>PPLG</c> a seat of government with no
        /// population of its own. Some are not settlements; the rest are not settlements OF AN ORDER, and folding them in
        /// would widen a band with places that never held that population. </summary>
        private static int OrderForFeatureCode(string code)
        {
            switch (code)
            {
                case "PPLC": return SettlementOrderCatalog.CapitalOrder;
                case "PPLA": return SettlementOrderCatalog.CapitalOrder;
                case "PPLA2": return SettlementOrderCatalog.CityOrder;
                case "PPLA3": return SettlementOrderCatalog.CityOrder;
                case "PPLA4": return SettlementOrderCatalog.TownOrder;
                case "PPLA5": return SettlementOrderCatalog.TownOrder;
                case "PPL": return PplFamilyOrder;
                case "PPLX": return PplFamilyOrder;
                default: return 0;
            }
        }


        /// <summary> Decides town from village for every <c>PPL</c>/<c>PPLX</c> place, at the median of that family's own
        /// population. At or above the median is a town, below it a village.
        ///
        /// The cut is the median rather than a number someone chose because GeoNames does not grade an unadministered
        /// place: <c>PPL</c> covers a hamlet and a market town alike. The median is the one split that needs no outside
        /// figure, and it says exactly what it is -- the upper half of the world's unadministered places against the
        /// lower half. The floor of the dump is a thousand people, so the village band this produces is the band of
        /// villages BIG ENOUGH TO BE LISTED, which is why the catalogue keeps its authored village floor rather than
        /// taking one from here. </summary>
        private static void SplitPplFamilyAtMedian(List<Place> places)
        {
            List<int> populations = new List<int>();
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].orderIndex == 0) { populations.Add(places[i].population); }
            }
            if (populations.Count == 0) { return; }
            populations.Sort();
            int median = populations[populations.Count / 2];
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].orderIndex != 0) { continue; }
                Place place = places[i];
                place.orderIndex = place.population >= median
                    ? SettlementOrderCatalog.TownOrder
                    : SettlementOrderCatalog.VillageOrder;
                places[i] = place;
            }
        }
        /// <summary> The places of one order in one bucket. The world bucket takes every member of the order. </summary>
        private static List<Place> Gather(List<Place> places, int orderIndex, string continentKey)
        {
            bool wholeWorld = continentKey == SettlementFitTable.WorldKey;
            List<Place> gathered = new List<Place>();
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].orderIndex != orderIndex) { continue; }
                if (!wholeWorld && places[i].continentKey != continentKey) { continue; }
                gathered.Add(places[i]);
            }
            return gathered;
        }


        /// <summary> Fits one row: the five population quantiles, the range actually observed, the three
        /// nearest-neighbour spacing quantiles and the rank-size exponent with its fit quality. </summary>
        private static SettlementFitRow FitRow(List<Place> bucket, int orderIndex, string continentKey)
        {
            List<int> populations = new List<int>(bucket.Count);
            for (int i = 0; i < bucket.Count; i++) { populations.Add(bucket[i].population); }
            populations.Sort();
            List<float> spacings = NearestNeighbourMetres(bucket);
            spacings.Sort();
            float exponent, fitQuality;
            FitRankSize(populations, out exponent, out fitQuality);
            SettlementFitRow row = new SettlementFitRow();
            row.orderIndex = orderIndex;
            row.continentKey = continentKey;
            row.recordCount = bucket.Count;
            row.populationP05 = Quantile(populations, 0.05f);
            row.populationP25 = Quantile(populations, 0.25f);
            row.populationP50 = Quantile(populations, 0.50f);
            row.populationP75 = Quantile(populations, 0.75f);
            row.populationP95 = Quantile(populations, 0.95f);
            row.populationObservedMin = populations[0];
            row.populationObservedMax = populations[populations.Count - 1];
            row.spacingP25Metres = Quantile(spacings, 0.25f);
            row.spacingP50Metres = Quantile(spacings, 0.50f);
            row.spacingP75Metres = Quantile(spacings, 0.75f);
            row.rankSizeExponent = exponent;
            row.rankSizeR2 = fitQuality;
            return row;
        }


        /// <summary> B-27 / D-6. Fits one RAIL row: the presence logistic, the station-count exponent and the three
        /// station-spacing quantiles. Returns null with a named reason when the sample cannot carry the fit.
        ///
        /// THE SAMPLE IS BUILT BY JOINING, and the join distance is the row's own search radius. Each place is asked
        /// whether a station lies within it; the answer is this place's observation. Five kilometres is the shipped
        /// default and it is a real choice: a station serving a settlement stands at its edge or inside it, and the
        /// largest shipped village is a few hundred metres across while a capital is tens of kilometres, so one distance
        /// cannot be right for both. It is deliberately generous rather than tight, because a station just outside a
        /// small town's boundary still serves that town, and the cost of being generous is a slightly optimistic curve
        /// while the cost of being tight is calling served places unserved.
        ///
        /// THE COUNT IS FITTED ONLY FROM SERVED PLACES, and it is fitted by regression on the logarithms rather than
        /// averaged. A power law through the origin is a straight line in log-log, so the exponent is a slope; averaging
        /// counts per band would give a step function that says nothing about the places between the bands. Places with
        /// one station are KEPT in that regression -- log(1) is zero, not undefined -- because dropping them would fit the
        /// curve to the exceptions.
        ///
        /// THE SPACING QUANTILES COME FROM SERVED PLACES WITH TWO OR MORE STATIONS. A place with one station has no
        /// spacing to contribute, and counting it as zero would drag the quantiles toward nothing. When too few
        /// multi-station places exist the spacing defaults are kept and the note says so, rather than fitting three
        /// quantiles from four numbers. </summary>
        private static SettlementRailFitRow FitRailRow(List<Place> bucket, List<GeoCoordinate> stations,
                                                       Dictionary<long, List<int>> stationGrid, int orderIndex,
                                                       string continentKey, out string reason)
        {
            reason = "";
            SettlementRailFitRow row = new SettlementRailFitRow();
            row.orderIndex = orderIndex;
            row.continentKey = continentKey;
            row.recordCount = bucket.Count;

            List<double> logPopulation = new List<double>(bucket.Count);
            List<bool> served = new List<bool>(bucket.Count);
            List<double> servedLogPopulation = new List<double>();
            List<double> servedLogCount = new List<double>();
            List<float> spacings = new List<float>();
            int withStation = 0;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].population <= 0) { continue; }
                List<float> nearby = StationsWithin(stations, stationGrid, bucket[i], row.stationSearchRadiusMetres);
                logPopulation.Add(Math.Log10(bucket[i].population));
                served.Add(nearby.Count > 0);
                if (nearby.Count == 0) { continue; }
                withStation++;
                // NATURAL logs on both sides of the count fit, deliberately unlike the presence fit's log10. The count
                // exponent is used by SettlementRailFitRow.StationCount as a power of a POPULATION RATIO, so it has to be
                // d(ln count) / d(ln population); fitting it against log10 population would inflate every exponent by
                // ln(10) and put four stations where the fit found two. The presence curve keeps log10 because its
                // coefficients are only ever read as a curve, never as a power.
                servedLogPopulation.Add(Math.Log(bucket[i].population));
                servedLogCount.Add(Math.Log(nearby.Count));
                if (nearby.Count >= 2)
                {
                    nearby.Sort();
                    // The typical gap between this place's own stations: the median of the distances from its centre out
                    // to each of them is a radius, not a gap, so the gap is taken as twice the median of the successive
                    // differences once they are sorted -- which for two stations either side of a centre is their
                    // separation. This is the only spacing the join can honestly yield, since a station point carries no
                    // route.
                    float gapSum = 0f;
                    for (int k = 1; k < nearby.Count; k++) { gapSum += nearby[k] - nearby[k - 1]; }
                    float gap = gapSum / (nearby.Count - 1);
                    if (gap > 0f) { spacings.Add(gap); }
                }
            }
            row.placesWithStation = withStation;
            if (logPopulation.Count < SettlementRailFitRow.MinimumRecordCount)
            {
                reason = "only " + logPopulation.Count + " of its places carried a population, fewer than the "
                         + SettlementRailFitRow.MinimumRecordCount + " a presence curve can be fitted from";
                return null;
            }
            int unserved = logPopulation.Count - withStation;
            if (withStation < SettlementRailFitRow.MinimumOnEachSide
                || unserved < SettlementRailFitRow.MinimumOnEachSide)
            {
                reason = withStation + " of its " + logPopulation.Count + " places had a station and " + unserved + " did"
                         + " not, and a logistic needs at least " + SettlementRailFitRow.MinimumOnEachSide + " of each --"
                         + " a sample that is all one answer has no curve, only a constant";
                return null;
            }

            double intercept, slope, quality;
            if (!FitLogistic(logPopulation, served, out intercept, out slope, out quality))
            {
                reason = "its presence curve did not converge, so no rail row was fitted rather than one holding whatever"
                         + " the solver had reached when it stopped";
                return null;
            }
            row.logisticIntercept = (float)intercept;
            row.logisticSlope = (float)slope;
            row.logisticFitQuality = (float)quality;

            if (servedLogPopulation.Count >= SettlementRailFitRow.MinimumOnEachSide)
            {
                double countSlope, countQuality;
                FitLogLog(servedLogPopulation, servedLogCount, out countSlope, out countQuality);
                // A slope outside (0, 1) is kept OUT of the row rather than written and refused later: the shipped
                // default is a real figure and a fitted nonsense is not an improvement on it.
                if (countSlope > 0.0 && countSlope < 1.0)
                {
                    row.stationCountExponent = (float)countSlope;
                    row.stationCountFitQuality = (float)countQuality;
                    row.stationCountReferencePopulation = 100000;
                    row.stationCountAtReference = CountAtReference(servedLogPopulation, servedLogCount, countSlope,
                                                                  row.stationCountReferencePopulation);
                }
            }

            if (spacings.Count >= SettlementRailFitRow.MinimumOnEachSide)
            {
                spacings.Sort();
                row.stationSpacingP25Metres = Quantile(spacings, 0.25f);
                row.stationSpacingP50Metres = Quantile(spacings, 0.50f);
                row.stationSpacingP75Metres = Quantile(spacings, 0.75f);
            }
            return row;
        }


        /// <summary> The distances, in metres, from one place out to every station inside a radius. A list rather than a
        /// count, because the count answers presence and the distances answer spacing, and asking twice would walk the
        /// grid twice.
        ///
        /// The ring width is derived from the radius rather than fixed, so a row whose search radius an author has widened
        /// to fifty kilometres still searches far enough. It uses the SMALLER of the two metres-per-degree at this
        /// latitude, for the reason <see cref="NearestMetres"/> gives: a ring count that is right at the equator is short
        /// where the meridians crowd. </summary>
        private static List<float> StationsWithin(List<GeoCoordinate> stations, Dictionary<long, List<int>> grid,
                                                  Place place, float radiusMetres)
        {
            List<float> found = new List<float>();
            if (radiusMetres <= 0f) { return found; }
            GeoCoordinate origin = new GeoCoordinate(place.latitudeDeg, place.longitudeDeg);
            double radians = Math.PI / 180.0;
            double metresPerDegreeNorth = GeoProjection.MeridionalRadius(place.latitudeDeg) * radians;
            double metresPerDegreeEast = GeoProjection.PrimeVerticalRadius(place.latitudeDeg)
                                         * Math.Cos(place.latitudeDeg * radians) * radians;
            double metresPerDegree = Math.Min(metresPerDegreeNorth, metresPerDegreeEast);
            if (metresPerDegree <= 0.0) { return found; }
            int rings = (int)Math.Ceiling(radiusMetres / (CellDegrees * metresPerDegree));
            if (rings < 1) { rings = 1; }
            if (rings > MaxRing) { rings = MaxRing; }
            long latCell = LatCellOf(place.latitudeDeg);
            long lonCell = LonCellOf(place.longitudeDeg);
            for (long dLat = -rings; dLat <= rings; dLat++)
            {
                for (long dLon = -rings; dLon <= rings; dLon++)
                {
                    List<int> cell;
                    long key = CellKeyOf(latCell + dLat, WrapLonCell(lonCell + dLon));
                    if (!grid.TryGetValue(key, out cell)) { continue; }
                    for (int k = 0; k < cell.Count; k++)
                    {
                        double metres = MetresBetweenCoordinates(origin, stations[cell[k]]);
                        if (metres <= radiusMetres) { found.Add((float)metres); }
                    }
                }
            }
            return found;
        }


        /// <summary> Metres between two coordinates, through GeoProjection's tangent plane, with the antimeridian handled
        /// the way <see cref="MetresBetween"/> handles it. A second method rather than a widened first one, because that
        /// one takes a <c>Place</c> and this takes a bare coordinate, and giving <c>Place</c> a fake population to reuse it
        /// would be worse than four lines. </summary>
        private static double MetresBetweenCoordinates(GeoCoordinate origin, GeoCoordinate other)
        {
            double longitude = other.longitudeDeg;
            double delta = longitude - origin.longitudeDeg;
            if (delta > 180.0) { longitude -= 360.0; }
            else if (delta < -180.0) { longitude += 360.0; }
            double east, north;
            GeoProjection.ToLocalMetres(new GeoCoordinate(other.latitudeDeg, longitude), origin, out east, out north);
            return Math.Sqrt((east * east) + (north * north));
        }
        /// <summary> The nearest-rank quantile of an ALREADY SORTED list.
        ///
        /// Nearest rank -- type 1 in Hyndman and Fan (1996) -- interpolates nothing, so every number the fitted table
        /// carries is a value some real place actually has and a band edge can be pointed at. Interpolating between two
        /// towns would invent a third. The empty list returning zero is not a quiet default either: a spacing quantile of
        /// zero fails <see cref="SettlementFitRow.IsShapeConsistent"/>, so a bucket whose places found no neighbour at all
        /// is refused by name instead of shipped. </summary>
        private static T Quantile<T>(List<T> sortedAscending, float fraction)
        {
            if (sortedAscending.Count == 0) { return default(T); }
            int index = Mathf.Clamp(Mathf.RoundToInt(fraction * (sortedAscending.Count - 1)), 0,
                sortedAscending.Count - 1);
            return sortedAscending[index];
        }
        /// <summary> Fits the rank-size exponent of a bucket by ordinary least squares on log population against log
        /// rank, and reports the coefficient of determination beside it.
        ///
        /// Zipf's law for cities (Zipf 1949; Gabaix 1999) holds that population falls as rank raised to a negative power,
        /// so the line is fitted in log-log and the exponent returned is the NEGATED slope: a positive number, and a
        /// larger one means the big places dominate more sharply. R squared travels with it because an exponent from a
        /// line that explains nothing is a number with no claim behind it -- which is exactly the distinction the order
        /// catalogue draws when it says the FORM is cited and the VALUE is fitted. </summary>
        private static void FitRankSize(List<int> sortedAscending, out float exponent, out float fitQuality)
        {
            exponent = 0f;
            fitQuality = 0f;
            int count = sortedAscending.Count;
            if (count < 2) { return; }
            double sumX = 0.0;
            double sumY = 0.0;
            for (int i = 0; i < count; i++)
            {
                sumX += Math.Log(i + 1);
                sumY += Math.Log(sortedAscending[count - 1 - i]);
            }
            double meanX = sumX / count;
            double meanY = sumY / count;
            double sxx = 0.0;
            double syy = 0.0;
            double sxy = 0.0;
            for (int i = 0; i < count; i++)
            {
                double dx = Math.Log(i + 1) - meanX;
                double dy = Math.Log(sortedAscending[count - 1 - i]) - meanY;
                sxx += dx * dx;
                syy += dy * dy;
                sxy += dx * dy;
            }
            if (sxx <= 0.0) { return; }
            exponent = (float)(-(sxy / sxx));
            fitQuality = syy <= 0.0 ? 0f : (float)((sxy * sxy) / (sxx * syy));
        }


        /// <summary> B-27 / D-6. Fits the two-parameter presence logistic by iteratively reweighted least squares, and
        /// reports McFadden's pseudo-R squared beside it. False when it did not converge.
        ///
        /// WHY NOT LEAST SQUARES ON THE PROPORTIONS. The obvious cheap fit is to bin the places by population, take the
        /// served fraction of each bin and fit a line to its log-odds. That fails on the bins that come out all-yes or
        /// all-no -- their log-odds is infinite -- and those are precisely the bins at the ends, which is where the curve's
        /// shape is decided. Maximum likelihood uses every place as its own observation and never forms a proportion, so it
        /// has no infinity to trip over.
        ///
        /// WHY IRLS AND NOT GRADIENT DESCENT. The logistic log-likelihood is concave in these two parameters, so there is
        /// exactly one maximum and no local optimum to land in; Newton's method on a concave function of two parameters
        /// converges in a handful of iterations with no step size to tune. Its update is the weighted normal equations,
        /// which for two parameters is a 2x2 solve written out in full below rather than a matrix library this package does
        /// not have. (McCullagh and Nelder 1989, chapter 2, is the standard treatment.)
        ///
        /// WHY McFADDEN AND NOT R SQUARED. There is no residual variance to explain -- the outcomes are zero and one -- so
        /// the honest quality figure compares the fitted log-likelihood against the log-likelihood of the intercept-only
        /// model: how much better than "the same probability everywhere" the curve does. It is not a proportion of
        /// variance and it is not comparable with <see cref="SettlementFitRow.rankSizeR2"/>; a value around 0.2 to 0.4 is
        /// a good logistic (McFadden 1974), which is why <see cref="SettlementRailFitRow"/> names the field
        /// <c>logisticFitQuality</c> rather than reusing the word R squared. </summary>
        private static bool FitLogistic(List<double> x, List<bool> y, out double intercept, out double slope,
                                        out double fitQuality)
        {
            intercept = 0.0;
            slope = 0.0;
            fitQuality = 0.0;
            int count = x.Count;
            if (count < 2 || y.Count != count) { return false; }

            // Started from the intercept-only solution -- the log-odds of the overall served rate, with a flat curve --
            // rather than from zero. That is the answer if population turns out not to matter, so the first step is
            // already sensible and a badly separated sample degrades toward a flat curve instead of wandering.
            int positives = 0;
            for (int i = 0; i < count; i++) { if (y[i]) { positives++; } }
            if (positives == 0 || positives == count) { return false; }
            double rate = positives / (double)count;
            double baseline = Math.Log(rate / (1.0 - rate));
            intercept = baseline;
            slope = 0.0;

            const int MaxIterations = 60;
            const double Tolerance = 1e-9;
            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                // The weighted normal equations for the two-parameter design [1, x]: gradient g and Hessian H.
                double g0 = 0.0, g1 = 0.0;
                double h00 = 0.0, h01 = 0.0, h11 = 0.0;
                for (int i = 0; i < count; i++)
                {
                    double eta = intercept + slope * x[i];
                    double p = 1.0 / (1.0 + Math.Exp(-Clamp(eta, -60.0, 60.0)));
                    double residual = (y[i] ? 1.0 : 0.0) - p;
                    double weight = p * (1.0 - p);
                    g0 += residual;
                    g1 += residual * x[i];
                    h00 += weight;
                    h01 += weight * x[i];
                    h11 += weight * x[i] * x[i];
                }
                double determinant = (h00 * h11) - (h01 * h01);
                // A vanishing determinant means the weights have all collapsed -- every fitted probability is at zero or
                // one, which is what a separable sample does. Refuse rather than divide by it.
                if (Math.Abs(determinant) < 1e-12) { return false; }
                double step0 = ((h11 * g0) - (h01 * g1)) / determinant;
                double step1 = ((h00 * g1) - (h01 * g0)) / determinant;
                intercept += step0;
                slope += step1;
                if (Math.Abs(step0) < Tolerance && Math.Abs(step1) < Tolerance)
                {
                    fitQuality = McFadden(x, y, intercept, slope, baseline);
                    return IsFinite(intercept) && IsFinite(slope);
                }
            }
            return false;
        }


        /// <summary> McFadden's pseudo-R squared: one minus the ratio of the fitted log-likelihood to the intercept-only
        /// log-likelihood. Clamped into [0, 1] because a converged fit cannot be worse than the flat curve it started
        /// from, and a tiny negative from floating-point round-off would read as a broken figure. </summary>
        private static double McFadden(List<double> x, List<bool> y, double intercept, double slope, double baseline)
        {
            double fitted = 0.0;
            double flat = 0.0;
            for (int i = 0; i < x.Count; i++)
            {
                fitted += LogLikelihood(intercept + slope * x[i], y[i]);
                flat += LogLikelihood(baseline, y[i]);
            }
            if (flat >= 0.0 || Math.Abs(flat) < 1e-12) { return 0.0; }
            double value = 1.0 - (fitted / flat);
            return Clamp(value, 0.0, 1.0);
        }


        /// <summary> The log-likelihood of one observation, written in the numerically safe form. <c>log(1 + exp(eta))</c>
        /// overflows for a large positive eta, so the identity <c>log(1 + exp(eta)) = eta + log(1 + exp(-eta))</c> is used
        /// on that side: both branches evaluate <c>exp</c> only on a non-positive argument, where it cannot
        /// overflow. </summary>
        private static double LogLikelihood(double eta, bool outcome)
        {
            double softplus = eta > 0.0 ? eta + Math.Log(1.0 + Math.Exp(-eta)) : Math.Log(1.0 + Math.Exp(eta));
            return (outcome ? eta : 0.0) - softplus;
        }


        /// <summary> B-27 / D-6. Ordinary least squares of one already-logged series on another, reporting the slope and
        /// its coefficient of determination. Separate from <see cref="FitRankSize"/> because that one manufactures its own
        /// x from the rank and negates its slope; this one is given both series and returns the slope as it is. </summary>
        private static void FitLogLog(List<double> x, List<double> y, out double slope, out double fitQuality)
        {
            slope = 0.0;
            fitQuality = 0.0;
            int count = x.Count;
            if (count < 2 || y.Count != count) { return; }
            double sumX = 0.0, sumY = 0.0;
            for (int i = 0; i < count; i++) { sumX += x[i]; sumY += y[i]; }
            double meanX = sumX / count;
            double meanY = sumY / count;
            double sxx = 0.0, syy = 0.0, sxy = 0.0;
            for (int i = 0; i < count; i++)
            {
                double dx = x[i] - meanX;
                double dy = y[i] - meanY;
                sxx += dx * dx;
                syy += dy * dy;
                sxy += dx * dy;
            }
            if (sxx <= 0.0) { return; }
            slope = sxy / sxx;
            fitQuality = syy <= 0.0 ? 0.0 : (sxy * sxy) / (sxx * syy);
        }


        /// <summary> The fitted station count at the reference population, read off the fitted line rather than averaged
        /// from the places near that population.
        ///
        /// Reading it off the line is what makes the row internally consistent:
        /// <see cref="SettlementRailFitRow.StationCount"/> evaluates a power law through this one point, so if the point
        /// came from somewhere other than the line the curve would not pass through its own reference. The intercept is
        /// recovered from the fitted slope and the two means, which is the definition of the least-squares line. Both
        /// series are NATURAL logs, matching the fit, so the exponentiation is a plain <c>Math.Exp</c> with no change of
        /// base to get wrong. Floored at one for the reason the row's own method is floored there. </summary>
        private static float CountAtReference(List<double> logPopulation, List<double> logCount, double slope,
                                              int referencePopulation)
        {
            if (logPopulation.Count == 0 || referencePopulation <= 0) { return 1f; }
            double sumX = 0.0, sumY = 0.0;
            for (int i = 0; i < logPopulation.Count; i++) { sumX += logPopulation[i]; sumY += logCount[i]; }
            double meanX = sumX / logPopulation.Count;
            double meanY = sumY / logPopulation.Count;
            double intercept = meanY - (slope * meanX);
            double predicted = Math.Exp(intercept + (slope * Math.Log(referencePopulation)));
            if (!IsFinite(predicted) || predicted < 1.0) { return 1f; }
            return (float)predicted;
        }


        private static double Clamp(double value, double low, double high)
        {
            return value < low ? low : (value > high ? high : value);
        }


        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        /// <summary> The distance in metres from every place in the bucket to the nearest OTHER place in the same bucket.
        ///
        /// This is the observable behind Christaller's spacing figure: how far apart places of one order actually sit. It
        /// is measured through <see cref="GeoProjection"/>, the WGS84 local tangent plane this package already uses for
        /// imported coordinates, rather than a second distance function written here. The plane is centred on the place
        /// being measured FROM, so at nearest-neighbour range its error is far below the width of a settlement.
        ///
        /// A one-degree grid keeps this to a search over neighbouring cells instead of a comparison of every pair: the
        /// village order alone holds on the order of a hundred thousand places, and every pair would be five thousand
        /// million comparisons. The ring widens until the best distance found is within the distance the ring is
        /// GUARANTEED to have covered, so what comes back is the true nearest neighbour and not merely the nearest one in
        /// the same cell. A place with nothing inside the widest ring contributes no spacing rather than a wrong one, and
        /// an exact coordinate duplicate is skipped rather than reported as a spacing of zero. </summary>
        private static List<float> NearestNeighbourMetres(List<Place> bucket)
        {
            Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < bucket.Count; i++)
            {
                long key = CellKeyOf(LatCellOf(bucket[i].latitudeDeg), LonCellOf(bucket[i].longitudeDeg));
                List<int> cell;
                if (!grid.TryGetValue(key, out cell))
                {
                    cell = new List<int>();
                    grid.Add(key, cell);
                }
                cell.Add(i);
            }
            List<float> spacings = new List<float>(bucket.Count);
            for (int i = 0; i < bucket.Count; i++)
            {
                double nearest = NearestMetres(bucket, grid, i);
                if (nearest > 0.0) { spacings.Add((float)nearest); }
            }
            return spacings;
        }
        /// <summary> The latitude cell of a coordinate, offset by ninety degrees so the index is never negative. </summary>
        private static long LatCellOf(double latitudeDeg)
        {
            return (long)Math.Floor((latitudeDeg + 90.0) / CellDegrees);
        }


        /// <summary> The longitude cell of a coordinate, offset by a hundred and eighty degrees. </summary>
        private static long LonCellOf(double longitudeDeg)
        {
            return (long)Math.Floor((longitudeDeg + 180.0) / CellDegrees);
        }


        /// <summary> One cell packed into one key, so a lookup allocates nothing. Longitude spans at most 360 cells at
        /// one degree, well inside the stride. </summary>
        private static long CellKeyOf(long latCell, long lonCell)
        {
            return (latCell * 100000L) + lonCell;
        }


        /// <summary> Brings a longitude cell back inside the world after a ring has stepped it past the antimeridian, so
        /// a place at 179 degrees east searches its true neighbours at 179 degrees west. </summary>
        private static long WrapLonCell(long lonCell)
        {
            long span = (long)Math.Round(360.0 / CellDegrees);
            return ((lonCell % span) + span) % span;
        }
        /// <summary> The nearest neighbour of one place, by rings of cells widening from its own.
        ///
        /// The stopping rule is what makes the answer exact. A ring of <c>r</c> cells around the place's own cell is
        /// guaranteed to cover everything within <c>r</c> cell widths of the PLACE, even when the place sits hard against
        /// its cell's edge, so once the best distance found is inside that guarantee no wider ring can better it. The
        /// guarantee is converted to metres with the smaller of the two metres-per-degree at this latitude -- north-south
        /// from the meridional radius, east-west from the prime-vertical radius narrowed by the cosine of latitude -- so
        /// it stays a guarantee where the meridians crowd together. </summary>
        private static double NearestMetres(List<Place> bucket, Dictionary<long, List<int>> grid, int index)
        {
            Place from = bucket[index];
            GeoCoordinate origin = new GeoCoordinate(from.latitudeDeg, from.longitudeDeg);
            long latCell = LatCellOf(from.latitudeDeg);
            long lonCell = LonCellOf(from.longitudeDeg);
            double radians = Math.PI / 180.0;
            double metresPerDegreeNorth = GeoProjection.MeridionalRadius(from.latitudeDeg) * radians;
            double metresPerDegreeEast = GeoProjection.PrimeVerticalRadius(from.latitudeDeg)
                                         * Math.Cos(from.latitudeDeg * radians) * radians;
            double metresPerDegree = Math.Min(metresPerDegreeNorth, metresPerDegreeEast);
            double best = 0.0;
            for (int ring = 1; ring <= MaxRing; ring++)
            {
                for (long dLat = -ring; dLat <= ring; dLat++)
                {
                    for (long dLon = -ring; dLon <= ring; dLon++)
                    {
                        // Rings after the first walk their perimeter only; the inside was searched already.
                        if (ring > 1 && Math.Abs(dLat) != ring && Math.Abs(dLon) != ring) { continue; }
                        List<int> cell;
                        long key = CellKeyOf(latCell + dLat, WrapLonCell(lonCell + dLon));
                        if (!grid.TryGetValue(key, out cell)) { continue; }
                        for (int k = 0; k < cell.Count; k++)
                        {
                            if (cell[k] == index) { continue; }
                            double metres = MetresBetween(origin, bucket[cell[k]]);
                            if (metres > 0.0 && (best <= 0.0 || metres < best)) { best = metres; }
                        }
                    }
                }
                if (best > 0.0 && best <= ring * CellDegrees * metresPerDegree) { return best; }
            }
            return best;
        }
        /// <summary> Metres between the place being measured from and another, through GeoProjection's tangent plane.
        ///
        /// The antimeridian is handled here rather than inside the projection: a pair either side of a hundred and eighty
        /// degrees is a few kilometres apart, so the other place is re-expressed as the same point on the near side. Left
        /// alone, the longitude difference would read as three hundred and fifty-nine degrees and the pair would look like
        /// the width of the world. </summary>
        private static double MetresBetween(GeoCoordinate origin, Place other)
        {
            double longitude = other.longitudeDeg;
            double delta = longitude - origin.longitudeDeg;
            if (delta > 180.0) { longitude -= 360.0; }
            else if (delta < -180.0) { longitude += 360.0; }
            double east, north;
            GeoProjection.ToLocalMetres(new GeoCoordinate(other.latitudeDeg, longitude), origin, out east, out north);
            return Math.Sqrt((east * east) + (north * north));
        }
        /// <summary> The report the window shows and the console keeps: what was read, what was used, and every world
        /// row's fitted numbers -- so "over a thousand real places" is a count on screen rather than a claim in a
        /// comment. </summary>
        private string BuildFitReport(int malformed, int notSettlements, List<string> notes)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.Append("Road & City Master: read ").Append(recordsRead.ToString("n0")).Append(" lines from ")
                .Append(dumpFileName).Append(" and fitted ").Append(fittedRows.Count).Append(" rows from ")
                .Append(recordsUsed.ToString("n0")).Append(" places. ").Append(malformed.ToString("n0"))
                .Append(" lines were not the published layout, and ").Append(notSettlements.ToString("n0"))
                .Append(" were rows this fit does not speak for -- a feature code outside the four orders, or no")
                .Append(" population to band.");
            for (int i = 0; i < fittedRows.Count; i++)
            {
                SettlementFitRow row = fittedRows[i];
                if (row.continentKey != SettlementFitTable.WorldKey) { continue; }
                text.Append("\n  world order ").Append(row.orderIndex).Append(": ")
                    .Append(row.recordCount.ToString("n0")).Append(" places, population ")
                    .Append(row.populationP05.ToString("n0")).Append(" / ").Append(row.populationP50.ToString("n0"))
                    .Append(" / ").Append(row.populationP95.ToString("n0"))
                    .Append(" at the 5th, 50th and 95th; spacing ").Append(row.spacingP25Metres.ToString("n0"))
                    .Append(" / ").Append(row.spacingP50Metres.ToString("n0")).Append(" / ")
                    .Append(row.spacingP75Metres.ToString("n0")).Append(" m at the 25th, 50th and 75th; rank-size")
                    .Append(" exponent ").Append(row.rankSizeExponent.ToString("0.###")).Append(" fitting ")
                    .Append(row.rankSizeR2.ToString("0.###")).Append(" of the variance");
            }
            if (notes.Count > 0)
            {
                int shown = notes.Count < 12 ? notes.Count : 12;
                text.Append("\n  no row fitted for: ");
                for (int i = 0; i < shown; i++)
                {
                    text.Append(i == 0 ? "" : "; ").Append(notes[i]);
                }
                if (notes.Count > shown) { text.Append("; and ").Append(notes.Count - shown).Append(" more"); }
            }
            text.Append("\n  The two allometric exponents are NOT fitted here. This dump carries no built-up area and no")
                .Append(" street length, so they stay authored in the order catalogue with their form cited and their")
                .Append(" value flagged.");
            text.Append("\n  The railway is NOT fitted here either. This dump carries no station of any kind, so the")
                .Append(" presence curve is fitted separately from allCountries.txt below -- until it is, the table's")
                .Append(" rail rows stay authored placeholders.");
            return text.ToString();
        }


        /// <summary> B-27 / D-6. The railway pass's report: what was read, what was joined, and every world rail row's
        /// fitted curve READ AS A CURVE. It prints the presence odds at four populations rather than the two coefficients,
        /// for the reason <see cref="SettlementRailFitRow.Describe"/> gives -- an intercept and a slope tell nobody
        /// anything, and this report is the only place an author can see whether the fit is plausible before writing it
        /// into an asset. </summary>
        private string BuildRailFitReport(int linesRead, List<string> notes)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.Append("Road & City Master: read ").Append(linesRead.ToString("n0")).Append(" lines from ")
                .Append(railDumpFileName).Append(", found ").Append(railStationsRead.ToString("n0"))
                .Append(" railway stations and stops, and fitted ").Append(railFittedRows.Count)
                .Append(" rail rows by joining them to the ").Append(fittedPlaces.Count.ToString("n0"))
                .Append(" places the population pass held.");
            for (int i = 0; i < railFittedRows.Count; i++)
            {
                SettlementRailFitRow row = railFittedRows[i];
                if (row.continentKey != SettlementFitTable.WorldKey) { continue; }
                text.Append("\n  world order ").Append(row.orderIndex).Append(": ")
                    .Append(row.placesWithStation.ToString("n0")).Append(" of ").Append(row.recordCount.ToString("n0"))
                    .Append(" places had a station within ").Append(row.stationSearchRadiusMetres.ToString("n0"))
                    .Append(" m. Presence ").Append(Percent(row.PresenceProbability(2000))).Append(" at 2,000 people, ")
                    .Append(Percent(row.PresenceProbability(20000))).Append(" at 20,000, ")
                    .Append(Percent(row.PresenceProbability(200000))).Append(" at 200,000, ")
                    .Append(Percent(row.PresenceProbability(2000000))).Append(" at 2,000,000 (pseudo-R2 ")
                    .Append(row.logisticFitQuality.ToString("0.###")).Append("). Stations ")
                    .Append(row.stationCountAtReference.ToString("0.#")).Append(" at ")
                    .Append(row.stationCountReferencePopulation.ToString("n0")).Append(" people rising as population to")
                    .Append(" the ").Append(row.stationCountExponent.ToString("0.###")).Append(" (R2 ")
                    .Append(row.stationCountFitQuality.ToString("0.###")).Append("); spacing ")
                    .Append(row.stationSpacingP25Metres.ToString("n0")).Append(" / ")
                    .Append(row.stationSpacingP50Metres.ToString("n0")).Append(" / ")
                    .Append(row.stationSpacingP75Metres.ToString("n0")).Append(" m at the 25th, 50th and 75th");
            }
            if (notes.Count > 0)
            {
                int shown = notes.Count < 12 ? notes.Count : 12;
                text.Append("\n  no rail row fitted for: ");
                for (int i = 0; i < shown; i++)
                {
                    text.Append(i == 0 ? "" : "; ").Append(notes[i]);
                }
                if (notes.Count > shown) { text.Append("; and ").Append(notes.Count - shown).Append(" more"); }
            }
            text.Append("\n  Route kilometres are NOT fitted here. A station is a point, and a dump of points carries no")
                .Append(" route length, so the track a settlement implies is arithmetic on the fitted count and the")
                .Append(" fitted spacing -- see SettlementRailFitRow.TrackLengthMetres for the arithmetic.");
            return text.ToString();
        }


        /// <summary> A probability as a percentage, for the report. </summary>
        private static string Percent(float probability)
        {
            return (probability * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
        /// <summary> Writes the fitted rows into the chosen table, or into a new asset when the author has chosen none.
        ///
        /// It fits a throwaway table first and validates THAT, so a fit that does not hold up never touches the author's
        /// asset. A fit table that fails its own rules is worse than no table at all: whatever read it would carry those
        /// numbers into an order catalogue and mark them measured. The asset write sits outside any undo group on purpose,
        /// because Unity cannot roll an asset creation back and offering a step that does nothing is worse than offering
        /// none. </summary>
        private string WriteFittedAsset()
        {
            if (fittedRows.Count == 0)
            {
                return "Road & City Master: nothing has been fitted yet, so there is nothing to write.";
            }
            SettlementFitTable candidate = ScriptableObject.CreateInstance<SettlementFitTable>();
            candidate.rows.Clear();
            for (int i = 0; i < fittedRows.Count; i++) { candidate.rows.Add(fittedRows[i]); }
            candidate.dumpFileName = dumpFileName;
            candidate.fittedOnUtc = fittedOnUtc;
            candidate.recordsRead = recordsRead;
            candidate.recordsUsed = recordsUsed;
            candidate.fittedFromRealDump = true;
            candidate.allometryFitted = false;
            // B-27 / D-6. The rail rows travel with the population rows when they exist and are simply absent when they do
            // not. NOT filled with authored placeholders here: an author who fitted the population dump and skipped the
            // railway file should get a table that says the railway question is unanswered, not one carrying four
            // authored curves under a provenance stamp that says this table was fitted.
            candidate.railRows.Clear();
            for (int i = 0; i < railFittedRows.Count; i++) { candidate.railRows.Add(railFittedRows[i]); }
            candidate.railFittedFromRealDump = railFittedRows.Count > 0;
            candidate.railDumpFileName = railFittedRows.Count > 0 ? railDumpFileName : "";
            candidate.railStationsRead = railFittedRows.Count > 0 ? railStationsRead : 0;
            string validation;
            if (!candidate.Validate(out validation))
            {
                DestroyImmediate(candidate);
                return "Road & City Master: nothing was written, because the fit does not hold up -- " + validation;
            }
            if (targetTable == null)
            {
                string path = EditorUtility.SaveFilePanelInProject("Save the fitted settlement table",
                    "SettlementFitTable", "asset", "The fitted table is saved into the project. The dump is not.");
                if (string.IsNullOrEmpty(path))
                {
                    DestroyImmediate(candidate);
                    return "Road & City Master: the save was cancelled, so nothing was written.";
                }
                AssetDatabase.CreateAsset(candidate, path);
                targetTable = candidate;
            }
            else
            {
                // The name is kept by hand: CopySerialized copies the object name too, and an object named differently
                // from the file it lives in reads as a broken asset.
                string keptName = targetTable.name;
                EditorUtility.CopySerialized(candidate, targetTable);
                targetTable.name = keptName;
                DestroyImmediate(candidate);
                EditorUtility.SetDirty(targetTable);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(validation);
            return "Road & City Master: wrote " + fittedRows.Count + " fitted rows and " + railFittedRows.Count
                   + " rail rows to " + AssetDatabase.GetAssetPath(targetTable) + ". " + validation;
        }
        /// <summary> Writes the placeholder table -- the one that ships with the package. It is derived from the order
        /// catalogue's own authored bands and marked as no measurement, so the package has a table to read before any
        /// author has downloaded anything, and nobody can mistake it for a fit. See
        /// <see cref="SettlementFitTable.CreateAuthored"/> for the shape it puts inside each band and why. </summary>
        private string WritePlaceholderAsset()
        {
            if (orderCatalog == null)
            {
                return "Road & City Master: the placeholder table is derived from an order catalogue, so pick one first.";
            }
            string catalogueReport;
            if (!orderCatalog.Validate(out catalogueReport))
            {
                return "Road & City Master: no placeholder table was written, because the order catalogue it would come"
                       + " from does not hold up -- " + catalogueReport;
            }
            SettlementFitTable table = SettlementFitTable.CreateAuthored(orderCatalog);
            string validation;
            if (!table.Validate(out validation))
            {
                DestroyImmediate(table);
                return "Road & City Master: no placeholder table was written -- " + validation;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save the placeholder settlement table",
                "SettlementFitTable", "asset", "This is the table that ships. It is derived, not measured.");
            if (string.IsNullOrEmpty(path))
            {
                DestroyImmediate(table);
                return "Road & City Master: the save was cancelled, so nothing was written.";
            }
            AssetDatabase.CreateAsset(table, path);
            AssetDatabase.SaveAssets();
            targetTable = table;
            Debug.Log(validation);
            return "Road & City Master: wrote the placeholder table to " + path + ", derived from "
                   + orderCatalog.name + " and marked as not fitted from a dump. " + validation;
        }
    }
}
#endif
