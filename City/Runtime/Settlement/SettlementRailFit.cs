#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-6. ONE ROW of the rail fit: what a dump of real places says about whether a settlement of one
    /// order in one continent bucket HAS a railway, how many stations it has, and how far apart they stand.
    ///
    /// It is a second row type beside <see cref="SettlementFitRow"/> rather than eight more fields on it, and the reason
    /// is the SOURCE. The population and spacing quantiles are fitted from GeoNames <c>cities1000</c>, which is a file of
    /// populated places. Whether a place has a station cannot be read from that file at all -- it carries population,
    /// position, feature code, elevation and time zone, and no railway. It is read instead from the same dump's
    /// <c>allCountries.txt</c>, filtered to the feature codes that ARE railway stations (<c>RSTN</c> a railroad station,
    /// <c>RSTP</c> a railroad stop), and then asked of each settlement as a distance question. Two files, two record
    /// counts, two "was this fitted" flags -- which is exactly the distinction <c>SettlementFitTable.allometryFitted</c>
    /// already draws for the allometric exponents, and the reason this row can be absent from a table whose population
    /// rows are present.
    ///
    /// WHAT IS FITTED HERE AND WHAT IS DERIVED FROM IT, because *"use real world math"* is only true of the first:
    ///
    ///   * FITTED -- <see cref="logisticIntercept"/> and <see cref="logisticSlope"/>, by logistic regression of station
    ///     presence on log population; <see cref="stationCountExponent"/> with its reference point, by least squares on
    ///     log station count against log population; and the three <see cref="stationSpacingP50Metres"/> quantiles, by
    ///     the same nearest-neighbour measurement the population rows use for settlement spacing.
    ///   * DERIVED -- the in-settlement track length, from a station count and a station spacing.
    ///     <see cref="TrackLengthMetres"/> is arithmetic on two fitted figures and says so; it is not a fit of route
    ///     kilometres, because station points carry no route length and a dump of points cannot be made to yield one.
    ///
    /// WHY A LOGISTIC AND NOT A THRESHOLD. *"some village may not have railways even some town but cities and bigcities
    /// or capital must have railway"* is a statement about a PROBABILITY that rises with size, and a threshold cannot say
    /// it: a cut-off at some population makes every place above it certain and every place below it impossible, so the
    /// sentence "some town may not" becomes false the moment the cut-off is drawn. A logistic curve is the standard model
    /// for a binary outcome against a continuous predictor (Cox 1958; Berkson 1944), it is monotone -- so a bigger place
    /// is never less likely to have rail than a smaller one -- and it is asymptotic at both ends, which is what makes a
    /// capital effectively always and a hamlet merely unlikely rather than banned. The predictor is log population rather
    /// than population because settlement size is heavy-tailed, which is the same reason
    /// <see cref="SettlementFitTable.CreateAuthored"/> spaces its quantiles geometrically.
    ///
    /// WHY IT IS NOT A COIN TOSS EITHER. The probability this row carries is turned into a yes or no by
    /// <see cref="SettlementRailPresence"/> against a stream derived from the city seed, so the same seed and the same
    /// table give the same answer for the same settlement every time. The randomness is in WHICH places of a band have
    /// rail, never in whether the band's rate is what the fit measured. </summary>
    [Serializable]
    public sealed class SettlementRailFitRow
    {
        /// <summary> Which order this row describes, matching <see cref="SettlementOrderClass.orderIndex"/>: 1 capital
        /// through 4 village. </summary>
        public int orderIndex = 1;

        /// <summary> Which continent bucket, or <see cref="SettlementFitTable.WorldKey"/> for the whole dump. Same keys
        /// as the population rows, from the same <see cref="SettlementFitTable.ContinentKeyFromTimeZone"/>, so a project
        /// filtering one bucket gets both fits for it or neither. </summary>
        public string continentKey = SettlementFitTable.WorldKey;

        /// <summary> How many SETTLEMENTS this row was fitted from -- not how many stations were read. A logistic fit is
        /// a fit over places, each contributing one yes or one no, so the places are the sample and the stations are the
        /// measurement made on them. </summary>
        public int recordCount;

        /// <summary> How many of those settlements had a station within reach. Kept beside
        /// <see cref="recordCount"/> rather than as a fraction because the PAIR is what
        /// <see cref="IsUsable"/> has to test: a fit in which every place has rail, or none does, is separable, and a
        /// separable logistic has no finite slope at all. A fraction would hide that by rounding to 1 or 0. </summary>
        public int placesWithStation;

        // ------------------------------------------------------------------ presence: FITTED logistic

        /// <summary> The intercept a in P(rail) = 1 / (1 + exp(-(a + b log10 P))). FITTED by iteratively reweighted
        /// least squares, the standard solver for this model.
        ///
        /// It is a large negative number in any real fit and that is not a bug: at log10 P = 0, which is one inhabitant,
        /// the model has to say almost never, and the slope has to carry it up to almost always by the time log10 P
        /// reaches 6. Reading the intercept alone tells an author nothing, which is why
        /// <see cref="Describe"/> reports the CURVE at four populations instead. </summary>
        public float logisticIntercept = -6f;

        /// <summary> The slope b on log10 population, from the same fit. Positive in any real fit -- a larger place is
        /// more likely to have a station -- and <see cref="IsShapeConsistent"/> refuses a negative one, because a
        /// negative slope says villages are the railway-served places and that is not a measurement of this world.
        /// </summary>
        public float logisticSlope = 1.4f;

        /// <summary> McFadden's pseudo-R squared for the presence fit: 1 - (log likelihood of the fitted model / log
        /// likelihood of a model that only knows the overall rate).
        ///
        /// It travels with the coefficients for the same reason <see cref="SettlementFitRow.rankSizeR2"/> travels with
        /// the rank-size exponent: a curve that explains nothing is a curve with no claim behind it. McFadden rather than
        /// an ordinary R squared because the outcome is binary and the residuals of a binary outcome are not normal, so
        /// the ordinary statistic has no interpretation here. Values are much lower than an ordinary R squared for a fit
        /// of the same quality -- 0.2 is a strong binary fit -- which is why the figure is named after its author in
        /// <see cref="Describe"/> rather than printed as "R2". </summary>
        public float logisticFitQuality;

        // ------------------------------------------------------------------ how many: FITTED allometry

        /// <summary> The exponent in station count proportional to P^e, fitted by least squares on log count against log
        /// population over the places of this order that HAVE at least one station.
        ///
        /// Sub-linear in any real fit, and for the same reason the built-area exponent is: a city of ten times the
        /// population does not have ten times the stations, it has a handful more on a network that serves a wider
        /// catchment per station. That is also, arithmetically, *"as for railways in cities do not need to be too much
        /// like road in cities just a few of them"* -- the sparseness is a consequence of a fitted exponent below one
        /// and not of a rule saying sparse. The street-length exponent is fitted from the same kind of law, so the two
        /// numbers are directly comparable and the gap between them IS the sparseness. </summary>
        public float stationCountExponent = 0.35f;

        /// <summary> The population the count reference below was measured at. A fit is a slope and a point, and this
        /// field with the next one is the point -- exactly the shape
        /// <see cref="SettlementOrderClass.referencePopulation"/> uses, so an author reads one idiom in both places
        /// rather than a coefficient here and a reference pair there. </summary>
        public int stationCountReferencePopulation = 100000;

        /// <summary> Station count AT <see cref="stationCountReferencePopulation"/>, as a real number rather than an
        /// integer: it is the fitted value of a curve at a point, and rounding it here would round the curve everywhere.
        /// <see cref="StationCount"/> is where a whole number is finally produced. </summary>
        public float stationCountAtReference = 2f;

        /// <summary> R squared of the count fit, on the log-log scale it was fitted on. </summary>
        public float stationCountFitQuality;

        // ------------------------------------------------------------------ how far apart: FITTED quantiles

        /// <summary> Nearest-neighbour distance between stations, in metres, at the 25th, 50th and 75th percentiles of
        /// this order's places, measured great-circle so a spacing measured across a continent is not distorted by a
        /// projection. The same measurement and the same three quantiles as
        /// <see cref="SettlementFitRow.spacingP25Metres"/>, on stations instead of on settlements.
        ///
        /// This is the figure that makes an in-settlement network a NETWORK rather than a count: two stations 900 m apart
        /// is a tramway, two stations 9 km apart is a mainline with a suburban halt, and both can hold two stations.
        /// </summary>
        public float stationSpacingP25Metres = 1200f;
        public float stationSpacingP50Metres = 2500f;
        public float stationSpacingP75Metres = 6000f;

        /// <summary> How far from a settlement's centre a station was allowed to count as ITS station, in metres, when
        /// this row was fitted. Recorded rather than assumed because it is the one judgement in the presence measurement
        /// and every figure in this row depends on it: widen it and more places have rail, narrow it and fewer do.
        ///
        /// It is stored per row so a reader can see what question was asked, and
        /// <see cref="SettlementRailPresence"/> never re-derives it. The importer sets it from the settlement's own
        /// order -- a capital's station may be ten kilometres from the middle of the capital and still be the capital's
        /// station, a village's may not -- so a single number here would be the wrong number for three of the four
        /// orders. </summary>
        public float stationSearchRadiusMetres = 5000f;

        // ------------------------------------------------------------------ readers: each law applied in one place

        /// <summary> The probability a settlement of this order and this population has a railway, by the fitted
        /// logistic. The one place the curve is evaluated, so a presence draw and a report sentence cannot disagree
        /// about what the fit says.
        ///
        /// A population at or below zero answers zero rather than clamping to the curve's left tail: nobody lives there,
        /// so the question has no answer and the honest one is no. The exponent is clamped to +-60 before
        /// <c>Math.Exp</c> because outside that range the double result is already 0 or 1 to every bit and the raw call
        /// overflows to infinity, which would make the division produce NaN for the very largest places -- the ones the
        /// answer is most obviously 1 for. </summary>
        public float PresenceProbability(int population)
        {
            if (population <= 0) { return 0f; }
            double exponent = logisticIntercept + logisticSlope * Math.Log10(population);
            if (exponent > 60.0) { return 1f; }
            if (exponent < -60.0) { return 0f; }
            return (float)(1.0 / (1.0 + Math.Exp(-exponent)));
        }

        /// <summary> How many stations a settlement of this order and population has, GIVEN that it has rail at all, by
        /// the fitted sub-linear allometry: the reference count scaled by the population ratio raised to
        /// <see cref="stationCountExponent"/>.
        ///
        /// It floors at one, and that is not a fudge -- it is what the conditional means. This function answers a
        /// question that only arises once <see cref="SettlementRailPresence"/> has decided the settlement is served, and
        /// a served settlement with no station is a contradiction. A village whose fitted count is 0.4 has one station,
        /// which is the real answer: the fit measured that most villages of its size have none, and the presence curve
        /// is where that was already said. </summary>
        public int StationCount(int population)
        {
            if (population <= 0 || stationCountReferencePopulation <= 0 || stationCountAtReference <= 0f)
            {
                return 1;
            }
            double ratio = population / (double)stationCountReferencePopulation;
            double count = stationCountAtReference * Math.Pow(ratio, stationCountExponent);
            int whole = (int)Math.Round(count, MidpointRounding.AwayFromZero);
            return whole < 1 ? 1 : whole;
        }

        /// <summary> The in-settlement track length in metres implied by a station count and the median station spacing:
        /// the gaps BETWEEN the stations, plus one spacing of approach at each end.
        ///
        /// DERIVED, and labelled so in the class header. A count of n stations has n-1 gaps between them, and a line that
        /// stopped dead at its outermost station would be a line with no way in or out of the settlement -- so the two
        /// end approaches are added, giving (n-1) + 2 = n+1 spacings. A single-station settlement therefore gets two
        /// spacings of track, which is a station with a line through it, and that is what a village halt is.
        ///
        /// It is NOT a fit of route kilometres and does not pretend to be: a dump of station points carries no route
        /// length, so fitting one from it would be inventing the variable. What this is instead is arithmetic on two
        /// figures that WERE measured, which is the same standing as
        /// <c>SettlementOrderClass.BuiltAreaM2</c> -- a law applied to a fitted point. </summary>
        public float TrackLengthMetres(int stationCount)
        {
            if (stationCount < 1 || stationSpacingP50Metres <= 0f) { return 0f; }
            return (stationCount + 1) * stationSpacingP50Metres;
        }

        /// <summary> Draws one spacing from this row's three quantiles, advancing the stream exactly once. On the ROW
        /// rather than on a resolver because the spacing distribution IS the row -- its three quantiles are the only
        /// thing the draw reads -- so a surface railway and an underground railway drawing spacing from their own rows
        /// call one method and cannot drift apart in how a quantile triple becomes a metre figure.
        ///
        /// THE QUANTILES ARE A SHAPE, NOT THREE CHOICES. Picking one of P25, P50 and P75 would give every settlement in
        /// the world one of three spacings, which is a distribution with three spikes in it and looks nothing like the
        /// sample it was fitted from. So the draw is uniform in the quantile domain and mapped through the quantiles
        /// piecewise: the lower half of the draws are spread between P25 and P50, the upper half between P50 and P75.
        /// That reproduces the fitted median exactly, keeps half the settlements inside the fitted interquartile range,
        /// and never invents a spacing outside the observed spread of the sample -- which a fitted lognormal, tempting as
        /// it is, would do at both ends from three numbers that cannot constrain a tail.
        ///
        /// The interpolation is GEOMETRIC, not arithmetic, because station spacings span better than an order of
        /// magnitude between a tramway and a rural branch and the fitted quantiles in this folder are treated as
        /// log-scaled everywhere else (<see cref="SettlementFitTable.CreateAuthored"/> makes the same argument about
        /// populations). Arithmetic interpolation between 1,200 m and 6,000 m would put far too many settlements near the
        /// upper end.
        ///
        /// It takes the stream by <c>ref</c> because <see cref="DeterministicRandom"/> is a struct that advances its own
        /// state, and a caller draws presence then spacing from ONE stream in a fixed order -- so this must advance the
        /// caller's stream, not a copy of it. </summary>
        public float DrawSpacing(ref DeterministicRandom random)
        {
            double u = random.NextDouble();
            double low, high, t;
            if (u < 0.5)
            {
                low = stationSpacingP25Metres;
                high = stationSpacingP50Metres;
                t = u * 2.0;
            }
            else
            {
                low = stationSpacingP50Metres;
                high = stationSpacingP75Metres;
                t = (u - 0.5) * 2.0;
            }
            if (low <= 0.0 || high <= 0.0) { return stationSpacingP50Metres; }
            return (float)(low * Math.Pow(high / low, t));
        }

        /// <summary> The fewest settlements a rail row may be fitted from. Twenty, matching
        /// <see cref="SettlementFitRow.MinimumRecordCount"/>, and it is the same constant repeated rather than a
        /// reference to it because the two rules coincide for DIFFERENT reasons and one changing should not move the
        /// other. There it is the count at which a 95th percentile exists; here it is the count below which a two-
        /// parameter logistic is fitting a curve through a handful of points and will happily separate them
        /// perfectly. </summary>
        public const int MinimumRecordCount = 20;

        /// <summary> The fewest places on each side of the outcome a logistic needs: five with a station and five
        /// without.
        ///
        /// A fit whose sample is all yes or all no is SEPARABLE, and a separable logistic has no maximum-likelihood
        /// solution at all -- the slope runs to infinity and any solver returns whatever it happened to reach when it
        /// stopped iterating. Five is the conventional floor of events per parameter in the epidemiological literature
        /// (Peduzzi et al. 1996 argues ten and notes five as the working minimum), and this model has two parameters, so
        /// five on the thinner side is the smallest sample that is fitting rather than interpolating. </summary>
        public const int MinimumOnEachSide = 5;

        /// <summary> Whether this row can be used AS A FIT, with the reason when it cannot. Asked before a presence draw
        /// is made from it, for the same reason <see cref="SettlementFitRow.IsUsable"/> is asked before a catalogue is
        /// written: a fit that failed quietly is worse than no fit, because a run would then carry a railway pattern
        /// nobody measured.
        ///
        /// Three things have to hold, and they are asked separately because they fail for separate reasons: enough places
        /// behind the row, enough places on each side of the outcome, and coefficients shaped like a fit rather than like
        /// a solver that gave up. </summary>
        public bool IsUsable(out string reason)
        {
            string label = "the fitted rail row for order " + orderIndex + " in " + continentKey;
            if (recordCount < MinimumRecordCount)
            {
                reason = label + " was fitted from " + recordCount + " settlements, and " + MinimumRecordCount
                         + " is the fewest a presence curve can be fitted to -- narrow the continent filter less, or fit"
                         + " the whole world instead";
                return false;
            }
            int without = recordCount - placesWithStation;
            if (placesWithStation < MinimumOnEachSide || without < MinimumOnEachSide)
            {
                reason = label + " saw " + placesWithStation + " places with a station and " + without + " without, and"
                         + " a presence curve needs at least " + MinimumOnEachSide + " of each: a sample that is all one"
                         + " answer has no curve through it, only a step";
                return false;
            }
            return IsShapeConsistent(out reason);
        }

        /// <summary> Whether the numbers in this row contradict each other, setting aside how many places produced them.
        /// Every rule here is one a real fit cannot break, so a failure is a fault in whatever wrote the row -- which is
        /// as true of an authored placeholder as it is of a measured fit, and is why this is asked separately from the
        /// counts above. </summary>
        public bool IsShapeConsistent(out string reason)
        {
            string label = "the fitted rail row for order " + orderIndex + " in " + continentKey;
            if (placesWithStation < 0 || recordCount < 0 || placesWithStation > recordCount)
            {
                reason = label + " says " + placesWithStation + " of " + recordCount + " places had a station, which"
                         + " cannot be counted from one sample";
                return false;
            }
            if (logisticSlope <= 0f)
            {
                reason = label + " fitted a presence slope of " + Figure(logisticSlope, "0.###")
                         + ", and a slope at or below zero says the SMALLER places are the railway-served ones -- which"
                         + " is the opposite of what a rail network does";
                return false;
            }
            if (stationCountExponent <= 0f || stationCountExponent >= 1f)
            {
                reason = label + " fitted a station-count exponent of " + Figure(stationCountExponent, "0.###")
                         + ", and it has to fall inside (0, 1): at or below zero a bigger city has fewer stations, and at"
                         + " or above one its stations grow at least as fast as its people, which is the one thing"
                         + " railways demonstrably do not do";
                return false;
            }
            if (stationCountReferencePopulation <= 0 || stationCountAtReference <= 0f)
            {
                reason = label + " carries a count reference of " + Figure(stationCountAtReference, "0.##")
                         + " stations at " + stationCountReferencePopulation + " people, and a fitted point needs a"
                         + " positive population and a positive count to be a point at all";
                return false;
            }
            if (stationSpacingP25Metres <= 0f || stationSpacingP25Metres > stationSpacingP50Metres
                || stationSpacingP50Metres > stationSpacingP75Metres)
            {
                reason = label + " carries station spacings that are not positive and rising ("
                         + Figure(stationSpacingP25Metres, "0") + ", " + Figure(stationSpacingP50Metres, "0") + ", "
                         + Figure(stationSpacingP75Metres, "0") + " m)";
                return false;
            }
            if (stationSearchRadiusMetres <= 0f)
            {
                reason = label + " records a station search radius of " + Figure(stationSearchRadiusMetres, "0")
                         + " m, and a radius at or below zero means no station could have counted for any place, so the"
                         + " presence figures cannot have come from this measurement";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> What this row says, as a sentence an author can check against the world they know.
        ///
        /// It reports the CURVE at four populations rather than its two coefficients, because an intercept of -6.2 and a
        /// slope of 1.4 tell nobody anything while "a village of 2,000 has rail 8% of the time and a city of 200,000 has
        /// it 96% of the time" is a claim a reader can agree or disagree with. The four populations are the shipped band
        /// edges of the four orders, so the sentence lands on the ladder the rest of the catalogue is built on. </summary>
        public string Describe()
        {
            int[] at = new int[] { 2000, 20000, 200000, 2000000 };
            string curve = "";
            for (int i = 0; i < at.Length; i++)
            {
                if (i > 0) { curve += ", "; }
                curve += at[i].ToString("n0") + ": " + Figure(PresenceProbability(at[i]) * 100f, "0.#") + "%";
            }
            return "Order " + orderIndex + " in " + continentKey + ", from " + recordCount + " settlements of which "
                   + placesWithStation + " had a station. Presence by population (" + curve
                   + "), McFadden pseudo-R2 " + Figure(logisticFitQuality, "0.###") + ". Stations scale as P^"
                   + Figure(stationCountExponent, "0.###") + " through "
                   + Figure(stationCountAtReference, "0.##") + " at "
                   + stationCountReferencePopulation.ToString("n0") + " people (R2 "
                   + Figure(stationCountFitQuality, "0.###") + "), spaced "
                   + Figure(stationSpacingP25Metres, "0") + " / " + Figure(stationSpacingP50Metres, "0") + " / "
                   + Figure(stationSpacingP75Metres, "0") + " m at the quartiles, measured within "
                   + Figure(stationSearchRadiusMetres, "0") + " m of a settlement centre.";
        }

        /// <summary> A figure formatted invariantly, so a sentence does not change shape in a comma-decimal
        /// locale. </summary>
        private static string Figure(float value, string format)
        {
            return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
