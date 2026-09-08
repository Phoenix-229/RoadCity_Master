#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-88: the real-world design defaults for every <see cref="Road.RoadClassEnum"/>, held as pure data
    /// so the lookup is testable without a scene. Each of the five headline numbers — lane width, lane count, shoulder
    /// width, design speed and max grade — carries a <see cref="NumberAudit"/> that either cites a named road-design
    /// reference (Matches) or states why the value is an authored selection within a real range (Authored); nothing is
    /// left unknown (C-15 / § 10.18). This register is the SINGLE source of those numbers: <see cref="Road.ApplyRoadClassDefaults(bool)"/>
    /// reads it rather than carrying its own copy, so the applied defaults and the audited defaults can never drift.
    /// The values below reproduce, exactly, the figures the road inspector applies.
    ///
    /// RM-110 (B-10) made the shoulder the fifth. It was the one dimensioned column with no citation at all, and it
    /// was the column an author complained about: a shoulder sized between an edge strip and a refuge is useless as
    /// either and reads, correctly, as a second lane. Citing it is what lets
    /// <c>City.RoadTypeChange.Overrides</c> state a band for it, so a shoulder widened until it looks like a lane is
    /// now something the table can be measured against rather than something nothing here was entitled to judge. </summary>
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
        public readonly NumberAudit ShoulderWidthCite;
        public readonly NumberAudit DesignSpeedCite;
        public readonly NumberAudit MaxGradeCite;

        public RoadClassProfile(Road.RoadClassEnum cls, int laneCount, double laneWidthM, bool shoulderEnabled,
            double shoulderWidthM, bool isOneWay, CenterDividerTypeEnum divider, double designSpeedKPH, double maxGrade,
            bool generatesMesh, NumberAudit laneWidthCite, NumberAudit laneCountCite, NumberAudit shoulderWidthCite,
            NumberAudit designSpeedCite, NumberAudit maxGradeCite)
        {
            Class = cls; LaneCount = laneCount; LaneWidthM = laneWidthM; ShoulderEnabled = shoulderEnabled;
            ShoulderWidthM = shoulderWidthM; IsOneWay = isOneWay; Divider = divider; DesignSpeedKPH = designSpeedKPH;
            MaxGrade = maxGrade; GeneratesMesh = generatesMesh; LaneWidthCite = laneWidthCite;
            LaneCountCite = laneCountCite; ShoulderWidthCite = shoulderWidthCite; DesignSpeedCite = designSpeedCite;
            MaxGradeCite = maxGradeCite;
        }

        /// <summary> True when the row is a complete default set: every dimensioned figure is present and positive and
        /// each of the five headline numbers is a well-formed audit row. Shoulder width is the one dimension allowed to
        /// be zero, because a carrier that builds no carriageway has no shoulder to size — but it still has to say so
        /// in an audit row, which is why the citation is required where the value is not. A class the register forgot
        /// returns <c>default</c>, whose LaneCount is 0, so it fails this — that is how the gate proves the lookup is
        /// total. </summary>
        public bool Complete
            => LaneCount >= 1 && LaneWidthM > 0 && ShoulderWidthM >= 0 && DesignSpeedKPH > 0 && MaxGrade > 0
               && LaneWidthCite.IsWellFormed() && LaneCountCite.IsWellFormed() && ShoulderWidthCite.IsWellFormed()
               && DesignSpeedCite.IsWellFormed() && MaxGradeCite.IsWellFormed();

        /// <summary> The five headline numbers as audit rows, in fixed order, for the traceability check. </summary>
        public IEnumerable<NumberAudit> Citations()
        {
            yield return LaneWidthCite;
            yield return LaneCountCite;
            yield return ShoulderWidthCite;
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

        // ---- RM-110 (B-10). The shoulder column, which until now carried no citation of any kind. ----

        internal const string RefShoulderRefuge =
            "AASHTO Green Book shoulder widths: 3.0 m (10 ft) is the minimum USABLE shoulder on a freeway and the "
            + "desirable figure on a high-volume arterial, because it is the width at which a stopped vehicle stands "
            + "CLEAR of the travelled way. The arithmetic behind it is the vehicle: AASHTO's SU and WB design vehicles "
            + "are 2.44 m wide and the widest legal vehicle is 2.6 m, so 3.0 m is that vehicle plus a margin at each "
            + "edge. This is why the figure does not climb with the class: what a refuge has to fit is the lorry that "
            + "stops in it, not the speed of the traffic going past.";

        internal const string RefShoulderStrip =
            "AASHTO Green Book: shoulders are normally OMITTED on a curbed urban street, where the space goes to the "
            + "curb, gutter and drainage, and rural collectors and local roads carry usable shoulders of 0.6-2.4 m "
            + "(2-8 ft) chosen by volume. A strip is an edge for the pavement to end against and for the edge line to "
            + "sit on, not a refuge -- so it is kept far narrower than the lane beside it, because a shoulder near lane "
            + "width reads as a lane and gets driven as one.";

        internal const string RefShoulderNone =
            "A rail, ropeway or footway carrier builds no carriageway, so it has no shoulder to size: the width is "
            + "zero rather than a figure that would be applied to a mesh which is never generated (C-15 AUTHORED).";

        internal const string RefRailGeometry =
            "Adhesion-railway geometry, not roadway geometry: line speed is set by the alignment, its superelevation "
            + "and the signalling, and the ruling grade by wheel-rail adhesion -- main lines are commonly held at or "
            + "under 2.5%, with steeper pitches on secondary and mountain lines. The per-class figure is an authored "
            + "selection within that practice (C-15 AUTHORED); the AASHTO road bands above do not govern a railway.";

        internal const string RefFootwayGrade =
            "A pedestrian access route under the US ADA/PROWAG rules is held to a 5% running slope, or 8.33% (1:12) "
            + "where it is a ramp with landings. This tool's footway carrier also stands for trails and park paths "
            + "that claim no accessible route, so the ceiling here is authored ABOVE the accessible figure and the "
            + "author tightens it to 0.05 on a path that must be accessible (C-15 AUTHORED).";

        // ---- Citation shorthands (the field name is written once here, not on every row). ----
        private static string V(double d) => d.ToString(CultureInfo.InvariantCulture);
        private static NumberAudit WMatch(double v, string r) => NumberAudit.Matches("laneWidthM", V(v), r);
        private static NumberAudit WAuth(double v, string r) => NumberAudit.Authored("laneWidthM", V(v), r);
        private static NumberAudit ShMatch(double v, string r) => NumberAudit.Matches("shoulderWidthM", V(v), r);
        private static NumberAudit ShAuth(double v, string r) => NumberAudit.Authored("shoulderWidthM", V(v), r);
        private static NumberAudit SAuth(double v, string r) => NumberAudit.Authored("designSpeedKPH", V(v), r);
        private static NumberAudit GMatch(double v, string r) => NumberAudit.Matches("maxGrade", V(v), r);
        private static NumberAudit GAuth(double v, string r) => NumberAudit.Authored("maxGrade", V(v), r);

        private static RoadClassProfile Row(Road.RoadClassEnum c, int lanes, double width, bool shE, double shW,
            bool oneWay, CenterDividerTypeEnum div, double speed, double grade, bool mesh,
            NumberAudit widthCite, NumberAudit shoulderCite, NumberAudit speedCite, NumberAudit gradeCite)
            => new RoadClassProfile(c, lanes, width, shE, shW, oneWay, div, speed, grade, mesh,
                widthCite,
                NumberAudit.Authored("laneCount", lanes.ToString(CultureInfo.InvariantCulture), RefLaneCount),
                shoulderCite, speedCite, gradeCite);

        // Rows reproduce the exact figures Road.ApplyRoadClassDefaults applies; the citations classify each headline
        // number as a cited real figure (Matches) or an authored selection within a real range (Authored).
        //
        // RM-110 (B-10) re-derived the shoulder column against RefShoulderRefuge / RefShoulderStrip. It is deliberately
        // a TWO-TIER column with nothing in between: 3.0 m where the class gives a stopped vehicle a refuge, 0.6-1.2 m
        // where the shoulder is only an edge strip, and 0 where the carrier builds no carriageway at all. The values
        // that used to sit in between -- 1.5 m on an urban four-lane, 2.0 m on a collector, 2.5 m on an arterial -- were
        // the defect the author reported: 2.5 m is a lorry's width with no clearance at all, so a vehicle stopped on it
        // still overhangs the traffic lane, while at 71% of the 3.5 m lane beside it the strip is wide enough to be read
        // as a lane and driven as one. A half-width shoulder is the one width that fails at both jobs.
        private static readonly RoadClassProfile[] _rows =
        {
            Row(Road.RoadClassEnum.UrbanSingleLane, 1, 3.0, false, 0.6, true, CenterDividerTypeEnum.None, 30, 0.12, true,
                WAuth(3.0, RefLaneWidthRange), ShMatch(0.6, RefShoulderStrip), SAuth(30, RefDesignSpeed), GAuth(0.12, RefGrade)),
            Row(Road.RoadClassEnum.UrbanTwoLane, 2, 3.25, true, 1.2, false, CenterDividerTypeEnum.Markers, 50, 0.10, true,
                WAuth(3.25, RefLaneWidthRange), ShAuth(1.2, RefShoulderStrip), SAuth(50, RefDesignSpeed), GAuth(0.10, RefGrade)),
            Row(Road.RoadClassEnum.OneWayStreet, 2, 3.25, true, 1.2, true, CenterDividerTypeEnum.None, 50, 0.10, true,
                WAuth(3.25, RefLaneWidthRange), ShAuth(1.2, RefShoulderStrip), SAuth(50, RefDesignSpeed), GAuth(0.10, RefGrade)),
            Row(Road.RoadClassEnum.UrbanFourLane, 4, 3.25, true, 1.2, false, CenterDividerTypeEnum.K_Rail, 60, 0.08, true,
                WAuth(3.25, RefLaneWidthRange), ShAuth(1.2, RefShoulderStrip), SAuth(60, RefDesignSpeed), GAuth(0.08, RefGrade)),
            Row(Road.RoadClassEnum.Collector, 4, 3.5, true, 1.2, false, CenterDividerTypeEnum.Markers, 70, 0.07, true,
                WAuth(3.5, RefLaneWidthRange), ShAuth(1.2, RefShoulderStrip), SAuth(70, RefDesignSpeed), GAuth(0.07, RefGrade)),
            Row(Road.RoadClassEnum.Arterial, 6, 3.5, true, 3.0, false, CenterDividerTypeEnum.K_Rail, 80, 0.06, true,
                WAuth(3.5, RefLaneWidthRange), ShMatch(3.0, RefShoulderRefuge), SAuth(80, RefDesignSpeed), GAuth(0.06, RefGrade)),
            Row(Road.RoadClassEnum.HighwayDual, 4, 3.65, true, 3.0, false, CenterDividerTypeEnum.K_Rail, 100, 0.05, true,
                WMatch(3.65, RefGreenBook), ShMatch(3.0, RefShoulderRefuge), SAuth(100, RefDesignSpeed), GMatch(0.05, RefGreenBook)),
            Row(Road.RoadClassEnum.Motorway, 6, 3.75, true, 3.0, false, CenterDividerTypeEnum.K_Rail, 120, 0.04, true,
                WMatch(3.75, RefGreenBook), ShMatch(3.0, RefShoulderRefuge), SAuth(120, RefDesignSpeed), GMatch(0.04, RefGreenBook)),
            Row(Road.RoadClassEnum.ServiceLane, 1, 3.0, false, 0.6, true, CenterDividerTypeEnum.None, 20, 0.15, true,
                WAuth(3.0, RefLaneWidthRange), ShMatch(0.6, RefShoulderStrip), SAuth(20, RefDesignSpeed), GAuth(0.15, RefGrade)),
            Row(Road.RoadClassEnum.RailSingle, 1, 1.435, false, 0, false, CenterDividerTypeEnum.None, 120, 0.035, false,
                WMatch(1.435, RefStandardGauge), ShAuth(0, RefShoulderNone), SAuth(120, RefRailGeometry), GAuth(0.035, RefRailGeometry)),
            Row(Road.RoadClassEnum.RailDouble, 2, 1.435, false, 0, false, CenterDividerTypeEnum.None, 120, 0.035, false,
                WMatch(1.435, RefStandardGauge), ShAuth(0, RefShoulderNone), SAuth(120, RefRailGeometry), GAuth(0.035, RefRailGeometry)),
            Row(Road.RoadClassEnum.Ropeway, 1, 1.0, false, 0, false, CenterDividerTypeEnum.None, 40, 0.60, false,
                WAuth(1.0, RefNonRoad), ShAuth(0, RefShoulderNone), SAuth(40, RefNonRoad), GAuth(0.60, RefNonRoad)),
            Row(Road.RoadClassEnum.PedestrianPath, 1, 2.0, false, 0, false, CenterDividerTypeEnum.None, 5, 0.12, false,
                WAuth(2.0, RefNonRoad), ShAuth(0, RefShoulderNone), SAuth(5, RefNonRoad), GAuth(0.12, RefFootwayGrade)),
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



