#region "Imports"
using System;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-8. The stopping-place FIGURES and the arithmetic on them, without a scene: how many bus
    /// stops a length of street frontage carries, how many bus stations an order gets, how far apart a rail service
    /// spaces its stations and how many fit a run, and whether a bus stop and a rail station are close enough to be
    /// one interchange. This is the half a gate can be trusted with. It computes and refuses; it never places a
    /// prefab, never touches a scene and never draws.
    ///
    /// What is DEFERRED, and why it is not here. Every stop in the spec is *"a slot the author's prefab fills"* and
    /// carries the pavement, shelter footprint, kerb build-out and crossing B-15 asks for -- and a bus stop belongs
    /// *"on the near side of a junction, clear of the crossing"*. None of that is a figure: it is placement against a
    /// road's frontage, a junction's geometry and a crossing's box, which is scene work in the builder's hands, the
    /// same division D-5's track and D-7's masts draw between the plan and the mesh. This file answers how many and
    /// how far apart; where each one lands is the builder's.
    ///
    /// It sits beside <see cref="TransportHub"/> on purpose. That file owns the CONTRACT a rail station or bus station
    /// must satisfy to stand at a site -- the road within reach, the forecourt depth, the water or the approach
    /// surface -- and validates one candidate against it. This file owns the COUNT and the SPACING that decide how
    /// many of those hubs a settlement gets and where along a line they fall. A site that this file spaces a station
    /// at still has to pass <see cref="TransportHub.Validate"/> before it is built; the two answer different
    /// questions and neither restates the other. </summary>
    public static class TransitPlacement
    {
        #region "Bus stops: spacing along a frontage, from the TCQSM"
        /// <summary> The two contexts the TCQSM spaces bus stops by. Not a taste setting: an urban street places stops
        /// closer because the walk to one is the whole trip's first cost, and a suburban road spaces them wider
        /// because the stops feed a longer ride and a stop every 200 m would halt the bus more than it moved it. </summary>
        public enum StopContext
        {
            Urban = 0,
            Suburban = 1
        }

        /// <summary> Where a bus stop spacing comes from, held as a band rather than one number because the TCQSM
        /// quotes a range and a street at the tight end of it is a real, different street from one at the wide end.
        /// The nominal is what a count is taken at; the min and max are what an author may move it between before the
        /// figure stops being the published one. AUTHORED (C-15) from the Transit Capacity and Quality of Service
        /// Manual's stop-spacing guidance -- representative values a project confirms, not invented ones. </summary>
        [Serializable]
        public sealed class BusStopSpacingClass
        {
            public float urbanNominalMetres = 200f;
            public float urbanMinMetres = 150f;
            public float urbanMaxMetres = 300f;

            public float suburbanNominalMetres = 350f;
            public float suburbanMinMetres = 300f;
            public float suburbanMaxMetres = 400f;

            public static BusStopSpacingClass Default() { return new BusStopSpacingClass(); }

            /// <summary> The nominal spacing for a context, in metres. The one place a caller turns a context into a
            /// distance, so a stop count and an interchange walk read the same figure. </summary>
            public float NominalMetres(StopContext context)
            {
                return context == StopContext.Suburban ? suburbanNominalMetres : urbanNominalMetres;
            }

            /// <summary> Whether the figures are a usable spacing band: every distance positive and each nominal
            /// inside its own min/max. A band that fails this cannot count stops, and the resolver refuses with the
            /// reason rather than dividing by a zero or a negative. </summary>
            public bool IsShapeConsistent(out string reason)
            {
                if (urbanMinMetres <= 0f || suburbanMinMetres <= 0f)
                {
                    reason = "a stop spacing is zero or negative, and a street cannot carry a stop every no metres";
                    return false;
                }
                if (urbanNominalMetres < urbanMinMetres || urbanNominalMetres > urbanMaxMetres)
                {
                    reason = "the urban nominal " + Figure(urbanNominalMetres) + " m is outside its own "
                             + Figure(urbanMinMetres) + " to " + Figure(urbanMaxMetres) + " m band";
                    return false;
                }
                if (suburbanNominalMetres < suburbanMinMetres || suburbanNominalMetres > suburbanMaxMetres)
                {
                    reason = "the suburban nominal " + Figure(suburbanNominalMetres) + " m is outside its own "
                             + Figure(suburbanMinMetres) + " to " + Figure(suburbanMaxMetres) + " m band";
                    return false;
                }
                reason = string.Empty;
                return true;
            }
        }

        /// <summary> How many bus stops a run of street frontage carries, and the spacing they fell out at.
        ///
        /// The count is the frontage divided by the nominal spacing, rounded to the nearest whole stop and never below
        /// one: a 120 m urban street shorter than a single 200 m interval still gets the one stop that serves it,
        /// because a street with a bus route and no stop is the one case the figure exists to prevent. The spacing
        /// returned is the frontage spread evenly across the stops actually placed -- the interval an author measures
        /// on the ground -- not the nominal it was counted from, because a 900 m street with four stops has them 225 m
        /// apart and reporting 200 m would describe a street this is not.
        ///
        /// This does NOT decide WHERE the stops sit. Near side of the junction, clear of the crossing, on the frontage
        /// and not in a passing lane -- all of that is the builder's, against the road and the junction. A frontage of
        /// zero or less refuses: a route with no street to run on is a caller's error, not a stop count. </summary>
        public static BusStopRun CountStopsAlongFrontage(double frontageMetres, StopContext context,
            BusStopSpacingClass spacing)
        {
            BusStopRun run = new BusStopRun();
            BusStopSpacingClass cls = spacing != null ? spacing : BusStopSpacingClass.Default();

            string reason;
            if (!cls.IsShapeConsistent(out reason))
            {
                run.refusal = "the bus stop spacing is unusable, so no stop count can be taken from it: " + reason;
                return run;
            }
            if (frontageMetres <= 0.0)
            {
                run.refusal = "the street frontage is " + Figure(frontageMetres)
                              + " m, so there is no route to place a stop on";
                return run;
            }

            double nominal = cls.NominalMetres(context);
            int count = (int)Math.Round(frontageMetres / nominal, MidpointRounding.AwayFromZero);
            if (count < 1) { count = 1; }

            run.resolved = true;
            run.stopCount = count;
            run.context = context;
            run.frontageMetres = frontageMetres;
            // One stop sits at the middle of the frontage, not at both ends, so the interval is the frontage over the
            // count, not over count - 1. Two stops on a 900 m street fall at 225 m and 675 m: 450 m apart, each 225 m
            // from its end, which is the even spread a rider actually walks.
            run.actualSpacingMetres = frontageMetres / count;
            return run;
        }
        #endregion


        #region "Bus stations: a count per settlement order"
        /// <summary> How many bus stations (stages) a settlement order gets. The author's own figures --
        /// *capital 5, big city 4, city 3, town 2, village 1* -- with the one adjustment the tool's own order ladder
        /// forces.
        ///
        /// THE LADDER HAS FOUR RUNGS AND THE FIGURE LIST HAS FIVE. <see cref="SettlementOrderCatalog"/> numbers the
        /// orders 1 capital, 2 city, 3 town, 4 village, and its rung 1 is displayed *"Capital / big city"* -- capital
        /// and big city are one rung in this tool, not two. So the *"big city 4"* line has no rung of its own to land
        /// on, and rung 1 takes the CAPITAL figure of 5: a merged capital/big-city rung is at least a capital, and
        /// rounding it down to the big-city figure would under-serve every actual capital. An author who wants big
        /// cities modelled apart from capitals splits the rung in the catalogue first; until then five figures map
        /// onto four rungs by dropping the one the ladder cannot express, and this comment is where that is stated so
        /// the missing 4 is a decision and not a lost figure.
        ///
        /// Returns zero for an order index off the ladder rather than guessing: an order this tool does not number is
        /// a caller's error, and a fabricated station count would hide it. </summary>
        public static int BusStationCount(int orderIndex)
        {
            switch (orderIndex)
            {
                case SettlementOrderCatalog.CapitalOrder: return 5; // capital, and the merged big-city rung with it
                case SettlementOrderCatalog.CityOrder: return 3;
                case SettlementOrderCatalog.TownOrder: return 2;
                case SettlementOrderCatalog.VillageOrder: return 1;
                default: return 0;
            }
        }
        #endregion


        #region "Rail stations: spacing per service, and how many fit a run"
        /// <summary> The three rail services the spec spaces stations by. A metro stops every kilometre because it
        /// serves the walk-up city; an intercity line runs tens of kilometres between stops because a stop is a town.
        /// The same track class can carry any of them -- the service is how OFTEN it stops, not what it is laid to --
        /// which is why this is its own enum and not a field on the track. </summary>
        public enum RailService
        {
            Metro = 0,
            Commuter = 1,
            Intercity = 2
        }

        /// <summary> The station-spacing band for each rail service, in metres. AUTHORED (C-15) from the spec's own
        /// figures: metro 0.6 - 1.2 km, commuter 2 - 5 km, intercity 20 - 60 km. Held as a band with a nominal for the
        /// same reason the bus stop spacing is -- the range is real and a line at either end of it is a different
        /// line -- and the nominal is the geometric middle of the band, not the arithmetic one, because station
        /// spacings span better than an order of magnitude and the arithmetic middle of 20 and 60 km sits far above
        /// the line an author pictures. </summary>
        [Serializable]
        public sealed class RailStationSpacingClass
        {
            public float metroMinMetres = 600f;
            public float metroMaxMetres = 1200f;
            public float commuterMinMetres = 2000f;
            public float commuterMaxMetres = 5000f;
            public float intercityMinMetres = 20000f;
            public float intercityMaxMetres = 60000f;

            public static RailStationSpacingClass Default() { return new RailStationSpacingClass(); }

            /// <summary> The min and max of a service's band. One accessor so a nominal and a refusal read the same
            /// two numbers. </summary>
            public void BandMetres(RailService service, out float min, out float max)
            {
                switch (service)
                {
                    case RailService.Metro: min = metroMinMetres; max = metroMaxMetres; return;
                    case RailService.Commuter: min = commuterMinMetres; max = commuterMaxMetres; return;
                    default: min = intercityMinMetres; max = intercityMaxMetres; return;
                }
            }

            /// <summary> The nominal spacing for a service: the GEOMETRIC middle of its band, sqrt(min*max), for the
            /// reason the class header gives. Zero when the band is unusable, which the resolver turns into a
            /// refusal. </summary>
            public float NominalMetres(RailService service)
            {
                float min, max;
                BandMetres(service, out min, out max);
                if (min <= 0f || max <= 0f) { return 0f; }
                return (float)Math.Sqrt((double)min * max);
            }

            /// <summary> Whether every band is positive and ordered min-below-max. A band that fails cannot space a
            /// station. </summary>
            public bool IsShapeConsistent(out string reason)
            {
                if (!BandOk(metroMinMetres, metroMaxMetres))
                {
                    reason = "the metro band is zero or inverted";
                    return false;
                }
                if (!BandOk(commuterMinMetres, commuterMaxMetres))
                {
                    reason = "the commuter band is zero or inverted";
                    return false;
                }
                if (!BandOk(intercityMinMetres, intercityMaxMetres))
                {
                    reason = "the intercity band is zero or inverted";
                    return false;
                }
                reason = string.Empty;
                return true;
            }

            private static bool BandOk(float min, float max) { return min > 0f && max > 0f && max >= min; }
        }

        /// <summary> How many stations a service places along a run of a given length, and the spacing they fell out
        /// at. The count is the fencepost of the run over the nominal spacing -- floor(length / spacing) intervals and
        /// one more station than intervals -- so a 4 km commuter line at 3.16 km nominal gets two stations, one at
        /// each end of its single interval, and never a station hanging off the end of the track the way an author's
        /// hand-placed <c>stationStopMetres</c> is checked against in D-5. A run shorter than one interval still gets
        /// the terminus pair that makes it a line rather than a single stop.
        ///
        /// This is the spacing authority for a line whose service is known; it does NOT overrule D-6, which decides
        /// how many stations a SETTLEMENT gets from the fitted presence curve. The two compose: D-6 says a city has
        /// three stations, this says how far apart three stations sit on this length of metro, and D-5 holds the
        /// author's own stops where they hand-placed them. </summary>
        public static RailStationRun CountStationsAlongRun(double runLengthMetres, RailService service,
            RailStationSpacingClass spacing)
        {
            RailStationRun run = new RailStationRun();
            RailStationSpacingClass cls = spacing != null ? spacing : RailStationSpacingClass.Default();

            string reason;
            if (!cls.IsShapeConsistent(out reason))
            {
                run.refusal = "the rail station spacing is unusable, so no station count can be taken from it: "
                              + reason;
                return run;
            }
            if (runLengthMetres <= 0.0)
            {
                run.refusal = "the run is " + Figure(runLengthMetres) + " m long, so there is no line to stop on";
                return run;
            }

            double nominal = cls.NominalMetres(service);
            if (nominal <= 0.0)
            {
                run.refusal = "the " + service + " band gives a spacing of zero, so a station count would divide by it";
                return run;
            }

            int intervals = (int)Math.Floor(runLengthMetres / nominal);
            if (intervals < 1) { intervals = 1; }

            run.resolved = true;
            run.service = service;
            run.runLengthMetres = runLengthMetres;
            run.stationCount = intervals + 1;
            run.actualSpacingMetres = runLengthMetres / intervals;
            return run;
        }
        #endregion


        #region "Interchange: a bus stop and a rail station close enough to be one place"
        /// <summary> The walking distance inside which a bus stop and a rail station are one interchange, in metres.
        /// AUTHORED (C-15) at 400 m -- the standard five-minute pedestrian catchment the TCQSM and most transit
        /// authorities plan bus access to a station within. Beyond it the two are separate stops that happen to be
        /// near each other, and the paved connection D-8 asks for would be a walk nobody makes. </summary>
        public const float DefaultInterchangeWalkMetres = 400f;

        /// <summary> Whether a bus stop and a rail station are close enough to be one interchange, and if so the
        /// length of the paved connection between them -- which is the distance itself, because the walk IS the
        /// connection the spec asks be built rather than left to coincidence.
        ///
        /// Pure and symmetric: which of the two is nearer the settlement centre does not change whether they
        /// interchange, only the walk between them. A negative walk budget refuses rather than accepting everything,
        /// because a walk budget below zero is a caller passing the wrong sign, not a very wide catchment. </summary>
        public static InterchangeResult ResolveInterchange(double busToRailMetres, double walkBudgetMetres)
        {
            InterchangeResult result = new InterchangeResult();
            if (walkBudgetMetres < 0.0)
            {
                result.refusal = "the interchange walk budget is " + Figure(walkBudgetMetres)
                                 + " m, which is not a distance a rider can walk";
                return result;
            }
            if (busToRailMetres < 0.0)
            {
                result.refusal = "the bus-to-rail distance is " + Figure(busToRailMetres)
                                 + " m, so the two stops were measured with the wrong sign";
                return result;
            }

            result.resolved = true;
            result.busToRailMetres = busToRailMetres;
            result.walkBudgetMetres = walkBudgetMetres;
            result.isInterchange = busToRailMetres <= walkBudgetMetres;
            result.pavedConnectionMetres = result.isInterchange ? busToRailMetres : 0.0;
            return result;
        }
        #endregion


        #region "Shared"
        private static string Figure(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
        #endregion
    }


    /// <summary> The result of counting bus stops along a frontage: the decision and the figures it was made from,
    /// carried together the way every computing type in this folder carries them, so a caller reading a count also
    /// holds the spacing that produced it and the refusal that would otherwise have been a silent zero. </summary>
    [Serializable]
    public struct BusStopRun
    {
        /// <summary> Whether the count could be taken. False means <see cref="refusal"/> is set and every figure below
        /// is meaningless -- NOT that the frontage carries no stop. </summary>
        public bool resolved;
        public string refusal;

        public int stopCount;
        public double frontageMetres;
        public double actualSpacingMetres;
        public TransitPlacement.StopContext context;

        public string Describe()
        {
            if (!resolved)
            {
                return "bus stops: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            return stopCount + (stopCount == 1 ? " bus stop" : " bus stops") + " along "
                   + frontageMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m of "
                   + (context == TransitPlacement.StopContext.Suburban ? "suburban" : "urban") + " frontage, "
                   + actualSpacingMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m apart.";
        }
    }


    /// <summary> The result of spacing rail stations along a run: the count, the service it was spaced for and the
    /// interval they fell out at. Refuses rather than returning a zero count, for the same reason the bus run
    /// does. </summary>
    [Serializable]
    public struct RailStationRun
    {
        public bool resolved;
        public string refusal;

        public int stationCount;
        public double runLengthMetres;
        public double actualSpacingMetres;
        public TransitPlacement.RailService service;

        public string Describe()
        {
            if (!resolved)
            {
                return "rail stations: cannot answer -- "
                       + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            return stationCount + (stationCount == 1 ? " station" : " stations") + " on "
                   + (runLengthMetres / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) + " km of " + service
                   + ", " + (actualSpacingMetres / 1000.0).ToString("0.##", CultureInfo.InvariantCulture)
                   + " km apart.";
        }
    }


    /// <summary> The result of testing a bus stop against a rail station for interchange: whether they are one place
    /// and the paved walk between them if they are. Carries the budget it was judged against so a near miss reads as
    /// a near miss and not as an unexplained no. </summary>
    [Serializable]
    public struct InterchangeResult
    {
        public bool resolved;
        public string refusal;

        public bool isInterchange;
        public double busToRailMetres;
        public double walkBudgetMetres;
        public double pavedConnectionMetres;

        public string Describe()
        {
            if (!resolved)
            {
                return "interchange: cannot answer -- "
                       + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            if (!isInterchange)
            {
                return "no interchange: " + busToRailMetres.ToString("0.#", CultureInfo.InvariantCulture)
                       + " m apart is beyond the " + walkBudgetMetres.ToString("0.#", CultureInfo.InvariantCulture)
                       + " m a rider walks.";
            }
            return "interchange: " + busToRailMetres.ToString("0.#", CultureInfo.InvariantCulture)
                   + " m apart, joined by a " + pavedConnectionMetres.ToString("0.#", CultureInfo.InvariantCulture)
                   + " m paved walk.";
        }
    }
}
