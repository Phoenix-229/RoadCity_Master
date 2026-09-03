#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-88: the real-world design defaults for every <see cref="Road.RoadClassEnum"/>, held as pure data
    /// so the lookup is testable without a scene. Each of the four headline numbers the task names — lane width, lane
    /// count, design speed and max grade — carries a <see cref="NumberAudit"/> that either cites a named road-design
    /// reference (Matches) or states why the value is an authored selection within a real range (Authored); nothing is
    /// left unknown (C-15 / § 10.18). This register is the SINGLE source of those numbers: <see cref="Road.ApplyRoadClassDefaults(bool)"/>
    /// reads it rather than carrying its own copy, so the applied defaults and the audited defaults can never drift.
    /// The values below reproduce, exactly, the figures the road inspector applies. </summary>
    public readonly struct RoadClassProfile
    {
        public readonly Road.RoadClassEnum Class;
        public readonly int LaneCount;
        public readonly double LaneWidthM;
        public readonly bool ShoulderEnabled;
        public readonly double ShoulderWidthM;
        public readonly bool IsOneWay;
        public readonly CenterDividerTypeEnum Divider;
        public readonly double DesignSpeedKPH;
        public readonly double MaxGrade;
        public readonly bool GeneratesMesh;

        public readonly NumberAudit LaneWidthCite;
        public readonly NumberAudit LaneCountCite;
        public readonly NumberAudit DesignSpeedCite;
        public readonly NumberAudit MaxGradeCite;

        public RoadClassProfile(Road.RoadClassEnum cls, int laneCount, double laneWidthM, bool shoulderEnabled,
            double shoulderWidthM, bool isOneWay, CenterDividerTypeEnum divider, double designSpeedKPH, double maxGrade,
            bool generatesMesh, NumberAudit laneWidthCite, NumberAudit laneCountCite, NumberAudit designSpeedCite,
            NumberAudit maxGradeCite)
        {
            Class = cls; LaneCount = laneCount; LaneWidthM = laneWidthM; ShoulderEnabled = shoulderEnabled;
            ShoulderWidthM = shoulderWidthM; IsOneWay = isOneWay; Divider = divider; DesignSpeedKPH = designSpeedKPH;
            MaxGrade = maxGrade; GeneratesMesh = generatesMesh; LaneWidthCite = laneWidthCite;
            LaneCountCite = laneCountCite; DesignSpeedCite = designSpeedCite; MaxGradeCite = maxGradeCite;
        }

        /// <summary> True when the row is a complete default set: every dimensioned figure is present and positive and
        /// each of the four headline numbers is a well-formed audit row. A class the register forgot returns
        /// <c>default</c>, whose LaneCount is 0, so it fails this — that is how the gate proves the lookup is total. </summary>
        public bool Complete
            => LaneCount >= 1 && LaneWidthM > 0 && DesignSpeedKPH > 0 && MaxGrade > 0
               && LaneWidthCite.IsWellFormed() && LaneCountCite.IsWellFormed()
               && DesignSpeedCite.IsWellFormed() && MaxGradeCite.IsWellFormed();

        /// <summary> The four headline numbers as audit rows, in fixed order, for the traceability check. </summary>
        public IEnumerable<NumberAudit> Citations()
        {
            yield return LaneWidthCite;
            yield return LaneCountCite;
            yield return DesignSpeedCite;
            yield return MaxGradeCite;
        }
    }


    /// <summary> The pure, deterministic lookup of <see cref="RoadClassProfile"/> per design class, and the one
    /// source the road inspector reads its defaults from. Rows are a fixed array in enum order (C-06). </summary>
    public static class RoadClassDefaults
    {
        // ---- Cited references / authored-selection notes (written once so the wording never drifts). ----

        internal const string RefGreenBook =
            "AASHTO 'A Policy on Geometric Design of Highways and Streets' (Green Book): 3.6-3.75 m freeway/highway "
            + "travel lanes and maximum grades that tighten with design speed (~4% motorway, ~5% freeway).";

        internal const string RefStandardGauge =
            "Standard-gauge railway track gauge is exactly 1,435 mm (1.435 m) — a real-world figure, not a tool "
            + "default; used here as the rail carrier's per-track width.";

        internal const string RefLaneWidthRange =
            "Travel-lane widths span ~3.0-3.75 m from urban-local to motorway (AASHTO Green Book / NACTO Urban Street "
            + "Design Guide); the per-class value here is an authored selection within that real range (C-15).";

        internal const string RefLaneCount =
            "Lane count is a per-class design decision, not a fixed dimensional standard; the count is the class's "
            + "default cross-section and the author overrides it per road (C-15 AUTHORED).";

        internal const string RefDesignSpeed =
            "Design/posted speed is set by jurisdiction and context (~20 km/h service lane to ~120 km/h motorway); the "
            + "per-class value is an authored default within that real range (C-15).";

        internal const string RefGrade =
            "Maximum longitudinal grade tightens with design speed — up to ~12-15% for local/service access, easing to "
            + "~6-8% for collectors/arterials (AASHTO Green Book); the per-class ceiling here is an authored selection "
            + "within that guidance (C-15).";

        internal const string RefNonRoad =
            "Ropeway and footway carriers adopt no roadway-geometry standard; their width, speed and grade are authored "
            + "for the tool's stylized non-carriageway carriers (C-15 AUTHORED).";

        // ---- Citation shorthands (the field name is written once here, not on every row). ----
        private static string V(double d) => d.ToString(CultureInfo.InvariantCulture);
        private static NumberAudit WMatch(double v, string r) => NumberAudit.Matches("laneWidthM", V(v), r);
        private static NumberAudit WAuth(double v, string r) => NumberAudit.Authored("laneWidthM", V(v), r);
        private static NumberAudit SAuth(double v, string r) => NumberAudit.Authored("designSpeedKPH", V(v), r);
        private static NumberAudit GMatch(double v, string r) => NumberAudit.Matches("maxGrade", V(v), r);
        private static NumberAudit GAuth(double v, string r) => NumberAudit.Authored("maxGrade", V(v), r);

        private static RoadClassProfile Row(Road.RoadClassEnum c, int lanes, double width, bool shE, double shW,
            bool oneWay, CenterDividerTypeEnum div, double speed, double grade, bool mesh,
            NumberAudit widthCite, NumberAudit speedCite, NumberAudit gradeCite)
            => new RoadClassProfile(c, lanes, width, shE, shW, oneWay, div, speed, grade, mesh,
                widthCite,
                NumberAudit.Authored("laneCount", lanes.ToString(CultureInfo.InvariantCulture), RefLaneCount),
                speedCite, gradeCite);

        // Rows reproduce the exact figures Road.ApplyRoadClassDefaults applies; the citations classify each headline
        // number as a cited real figure (Matches) or an authored selection within a real range (Authored).
        private static readonly RoadClassProfile[] _rows =
        {
            Row(Road.RoadClassEnum.UrbanSingleLane, 1, 3.0, false, 0.5, true, CenterDividerTypeEnum.None, 30, 0.12, true,
                WAuth(3.0, RefLaneWidthRange), SAuth(30, RefDesignSpeed), GAuth(0.12, RefGrade)),
            Row(Road.RoadClassEnum.UrbanTwoLane, 2, 3.25, true, 1.0, false, CenterDividerTypeEnum.Markers, 50, 0.10, true,
                WAuth(3.25, RefLaneWidthRange), SAuth(50, RefDesignSpeed), GAuth(0.10, RefGrade)),
            Row(Road.RoadClassEnum.OneWayStreet, 2, 3.25, true, 1.0, true, CenterDividerTypeEnum.None, 50, 0.10, true,
                WAuth(3.25, RefLaneWidthRange), SAuth(50, RefDesignSpeed), GAuth(0.10, RefGrade)),
            Row(Road.RoadClassEnum.UrbanFourLane, 4, 3.25, true, 1.5, false, CenterDividerTypeEnum.K_Rail, 60, 0.08, true,
                WAuth(3.25, RefLaneWidthRange), SAuth(60, RefDesignSpeed), GAuth(0.08, RefGrade)),
            Row(Road.RoadClassEnum.Collector, 4, 3.5, true, 2.0, false, CenterDividerTypeEnum.Markers, 70, 0.07, true,
                WAuth(3.5, RefLaneWidthRange), SAuth(70, RefDesignSpeed), GAuth(0.07, RefGrade)),
            Row(Road.RoadClassEnum.Arterial, 6, 3.5, true, 2.5, false, CenterDividerTypeEnum.K_Rail, 80, 0.06, true,
                WAuth(3.5, RefLaneWidthRange), SAuth(80, RefDesignSpeed), GAuth(0.06, RefGrade)),
            Row(Road.RoadClassEnum.HighwayDual, 4, 3.65, true, 3.0, false, CenterDividerTypeEnum.K_Rail, 100, 0.05, true,
                WMatch(3.65, RefGreenBook), SAuth(100, RefDesignSpeed), GMatch(0.05, RefGreenBook)),
            Row(Road.RoadClassEnum.Motorway, 6, 3.75, true, 3.5, false, CenterDividerTypeEnum.K_Rail, 120, 0.04, true,
                WMatch(3.75, RefGreenBook), SAuth(120, RefDesignSpeed), GMatch(0.04, RefGreenBook)),
            Row(Road.RoadClassEnum.ServiceLane, 1, 3.0, false, 0.5, true, CenterDividerTypeEnum.None, 20, 0.15, true,
                WAuth(3.0, RefLaneWidthRange), SAuth(20, RefDesignSpeed), GAuth(0.15, RefGrade)),
            Row(Road.RoadClassEnum.RailSingle, 1, 1.435, false, 0.5, false, CenterDividerTypeEnum.None, 120, 0.035, false,
                WMatch(1.435, RefStandardGauge), SAuth(120, RefDesignSpeed), GAuth(0.035, RefGrade)),
            Row(Road.RoadClassEnum.RailDouble, 2, 1.435, false, 0.5, false, CenterDividerTypeEnum.None, 120, 0.035, false,
                WMatch(1.435, RefStandardGauge), SAuth(120, RefDesignSpeed), GAuth(0.035, RefGrade)),
            Row(Road.RoadClassEnum.Ropeway, 1, 1.0, false, 0, false, CenterDividerTypeEnum.None, 40, 0.60, false,
                WAuth(1.0, RefNonRoad), SAuth(40, RefNonRoad), GAuth(0.60, RefNonRoad)),
            Row(Road.RoadClassEnum.PedestrianPath, 1, 2.0, false, 0, false, CenterDividerTypeEnum.None, 5, 0.12, false,
                WAuth(2.0, RefNonRoad), SAuth(5, RefNonRoad), GAuth(0.12, RefNonRoad)),
        };

        /// <summary> Every profile in fixed enum order. </summary>
        public static IReadOnlyList<RoadClassProfile> All() => _rows;

        /// <summary> The default set for a class. A class the register does not carry returns <c>default</c> (an
        /// incomplete profile), which the gate's totality case reports rather than throwing. </summary>
        public static RoadClassProfile For(Road.RoadClassEnum c)
        {
            for (int i = 0; i < _rows.Length; i++) { if (_rows[i].Class == c) return _rows[i]; }
            return default;
        }

        /// <summary> Whether the road's <paramref name="current"/> value is an author override rather than a value
        /// still sitting at the OUTGOING class's default. One owner for the question, so the merge below and RM-103's
        /// override report can never disagree about what counts as changed.
        ///
        /// The comparison happens at FLOAT resolution because that is the precision a road actually stores these in.
        /// Asking the question in double precision was wrong in a way that only showed up on a round trip: a highway's
        /// cited 3.65 m lane, written to the road's float field and read back, differs from the table's double by about
        /// 1e-7 m, so a value the merge had merely refreshed looked like an author override on the way back and was
        /// kept — an arterial changed to a highway and back was no longer the arterial it started as. A tenth of a
        /// micron is not a design decision anybody makes; a float that differs at all is. </summary>
        public static bool IsOverride(double previousDefault, double current)
            => (float)current != (float)previousDefault;

        /// <summary> Pure override-preserving merge for one numeric field: if the road's <paramref name="current"/>
        /// value differs from the OUTGOING class's default, the author changed it and it is kept; otherwise it is
        /// refreshed to the INCOMING class's <paramref name="newDefault"/>. This is how selecting a class populates
        /// defaults without overwriting an explicit override, and it is testable without a scene. </summary>
        public static double MergeField(double previousDefault, double current, double newDefault)
            => IsOverride(previousDefault, current) ? current : newDefault;

        /// <summary> A deterministic tab-separated dump of the whole lookup, for the gate's C-06 repeat check. </summary>
        public static string ToTable()
        {
            StringBuilder sb = new StringBuilder();
            foreach (RoadClassProfile p in _rows)
            {
                sb.Append(p.Class).Append('\t').Append(p.LaneCount).Append('\t').Append(V(p.LaneWidthM)).Append('\t')
                  .Append(p.ShoulderEnabled ? 1 : 0).Append('\t').Append(V(p.ShoulderWidthM)).Append('\t')
                  .Append(p.IsOneWay ? 1 : 0).Append('\t').Append(p.Divider).Append('\t').Append(V(p.DesignSpeedKPH))
                  .Append('\t').Append(V(p.MaxGrade)).Append('\t').Append(p.GeneratesMesh ? 1 : 0).Append('\n');
            }
            return sb.ToString();
        }
    }
}



