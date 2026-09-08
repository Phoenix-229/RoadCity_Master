#region "Imports"
using System;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-9. THE ANSWER, for one settlement, to the three questions the underground register asks: does
    /// this place have a metro, does its ground force an underground ROAD, and -- if it has a metro -- can the portal
    /// that metro needs actually be cut. One struct, returned by <see cref="SettlementUndergroundPresence.Resolve"/>,
    /// carrying each answer with the figures it was made from, because the three are decided by three different kinds of
    /// evidence and an author debugging one must not be shown only a verdict on all three.
    ///
    /// THE THREE ANSWERS ARE INDEPENDENT AND ARE KEPT SO. Metro presence is a fitted probability against population;
    /// underground-road presence is a physical reading of relief and water; portal-cuttability is a physical reading of
    /// the ground the portal falls on. A missing fit table silences the first without touching the other two, and a
    /// flat inland city answers no to the road while the fit still answers the metro -- so each part carries its own
    /// resolved/refusal rather than one verdict standing in for all three. </summary>
    [Serializable]
    public struct SettlementUndergroundReport
    {
        /// <summary> Whether the resolver could run at all. False only when the settlement figure itself is unusable --
        /// see <see cref="refusal"/> -- NOT when one of the three parts came out no or one part could not be answered;
        /// those are read from the parts below. </summary>
        public bool resolved;

        /// <summary> Why the resolver could not run, empty when it could. </summary>
        public string refusal;

        // ---------------------------------------------------------------- metro: the fitted underground railway

        /// <summary> The metro answer, from the same fitted logistic that decides surface rail, drawn against the fit
        /// table's UNDERGROUND rows through <see cref="SettlementRailPresence.RailMode.Underground"/>. It carries its own
        /// <see cref="SettlementRailReport.resolved"/> and <see cref="SettlementRailReport.refusal"/>: a table with no
        /// metro rows leaves this unresolved while the road answer below still stands. Its station count, spacing and
        /// track length ARE the alignment and the box count this task supplies to B-18 -- the line is that many boxes,
        /// that far apart, over that much bored length. </summary>
        public SettlementRailReport metro;

        // ---------------------------------------------------------------- underground road: the physical answer

        /// <summary> Whether the ground forces an underground road here, decided physically and never by a draw: a city
        /// builds one only where the surface alternative is defeated, so this is true only when
        /// <see cref="reliefForcesUnderground"/> or <see cref="waterGapForcesUnderground"/> is. A flat inland city with
        /// an easy grid and no water gap answers false, which is the real answer. </summary>
        public bool undergroundRoadPresent;

        /// <summary> How hard the surface road would find the ground, as the suitability raster's own
        /// <see cref="SuitabilityFactors.SlopeResistance"/>: zero on the flat, one at the road class's ruling gradient,
        /// quadratic between. It is the relief the surface road would have to climb, read on the same 0..1 scale the
        /// cost surface already uses, so a reader compares it against the map they already have. </summary>
        public double reliefSeverity;

        /// <summary> True when the relief alone forces the road under: the surface grade is within
        /// <see cref="SettlementUndergroundPresence.ReliefTunnelSeverity"/> of the class limit, i.e. the surface road is
        /// at or near the steepest it is allowed to be and a tunnel is the real alternative rather than a switchback.
        /// </summary>
        public bool reliefForcesUnderground;

        /// <summary> True when a water crossing the surface network cannot span forces the road under: a positive gap
        /// wider than the longest structure the surface bridge catalogue offers. Answered no when either figure is
        /// missing, because "the surface cannot cross" is a claim that needs both the gap and the surface's reach to be
        /// known. </summary>
        public bool waterGapForcesUnderground;

        /// <summary> What the road answer rests on, in words: which of the two reasons fired, or why neither did.
        /// </summary>
        public string undergroundRoadReason;

        // ---------------------------------------------------------------- portal: the surface must agree

        /// <summary> Whether the metro's portal can be cut where it falls. Meaningful only when the metro is served;
        /// true and empty otherwise. When false the metro is REFUSED for building -- see <see cref="portalRefusal"/> --
        /// rather than buried and left invisible, which is the whole of "the surface must agree with what is beneath
        /// it". The cut itself is B-18's; this only decides whether B-18 could make it. </summary>
        public bool portalCuttable;

        /// <summary> Why the portal cannot be cut, empty when it can or when there is no metro to cut one for. </summary>
        public string portalRefusal;

        /// <summary> Whether a metro actually gets built here: the fit said yes AND the portal can be cut. This is the
        /// figure a builder acts on; <see cref="SettlementRailReport.served"/> is only the fit's half of it. </summary>
        public bool metroBuildable;

        /// <summary> The station box this task supplies for B-18 to cut and the author's prefab to fill: platform length,
        /// box width and depth below the surface, in metres. Standard authored metro dimensions, the same for every box,
        /// carried even when unbuildable so a report reads the same shape throughout. </summary>
        public float stationBoxLengthMetres;
        public float stationBoxWidthMetres;
        public float stationBoxDepthMetres;

        /// <summary> One line, in the register's voice: the metro, the road and, when a metro is served but cannot be
        /// portalled, the refusal. It reports the metro as a METRO rather than borrowing
        /// <see cref="SettlementRailReport.Describe"/>'s "railway" wording, because an author reading an underground
        /// readout should see the word for the thing they asked for. </summary>
        public string Describe()
        {
            if (!resolved)
            {
                return "underground: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }

            string metroLine;
            if (!metro.resolved)
            {
                metroLine = "metro: cannot answer -- "
                            + (string.IsNullOrEmpty(metro.refusal) ? "no reason given" : metro.refusal);
            }
            else
            {
                string basis = (metro.fromFittedCurve ? "fitted" : "AUTHORED PLACEHOLDER") + " curve for order "
                               + metro.orderIndex + " in " + metro.continentKey
                               + (metro.usedWorldFallback ? " (world fallback)" : "");
                string odds = Figure(metro.presenceProbability * 100f, "%") + " chance at "
                              + metro.population.ToString("N0", CultureInfo.InvariantCulture) + " people, drew "
                              + Figure(metro.presenceDraw * 100f, "%");
                if (!metro.served)
                {
                    metroLine = "metro: none. " + odds + ". " + basis + ".";
                }
                else
                {
                    metroLine = "metro: " + metro.stationCount + (metro.stationCount == 1 ? " station" : " stations")
                                + " about " + Figure(metro.stationSpacingMetres, " m") + " apart, "
                                + Figure(metro.trackLengthMetres, " m") + " of tunnel in boxes "
                                + Figure(stationBoxLengthMetres, " m") + " long. " + odds + ". " + basis + ".";
                    metroLine += portalCuttable
                        ? " Portal cuttable, metro buildable."
                        : " PORTAL REFUSED: " + portalRefusal;
                }
            }

            string roadLine = undergroundRoadPresent
                ? "underground road: yes -- " + undergroundRoadReason + "."
                : "underground road: none -- " + undergroundRoadReason + ".";

            return metroLine + " " + roadLine;
        }

        private static string Figure(float value, string unit)
        {
            return value.ToString(Math.Abs(value) < 10f ? "0.0" : "0", CultureInfo.InvariantCulture) + unit;
        }
    }


    /// <summary> B-27 / D-9. Turns one settlement's figures and the physical readings around it into a
    /// <see cref="SettlementUndergroundReport"/>: whether it has a metro, whether its ground forces an underground road,
    /// and whether the metro's portal can be cut. A static resolver on the folder's doctrine -- it computes and refuses,
    /// it never mutates a scene, never touches an asset and never draws.
    ///
    /// WHAT IS REAL MATH HERE AND WHAT IS DEFERRED, because *"use real world math"* is the whole point:
    ///
    ///   * METRO PRESENCE is the fitted logistic of D-6, evaluated against the fit table's underground rows and drawn
    ///     against a per-settlement seeded stream -- reproducible, monotone in population, asymptotic at both ends. It is
    ///     <see cref="SettlementRailPresence.Resolve"/> in <see cref="SettlementRailPresence.RailMode.Underground"/>, not
    ///     a second copy of the draw, so a metro and a surface railway cannot disagree about how a curve becomes a yes.
    ///   * UNDERGROUND-ROAD PRESENCE is physics, not a draw: the surface relief the road would climb (the suitability
    ///     raster's <see cref="SuitabilityFactors.SlopeResistance"/>) and a water crossing the surface network cannot
    ///     make. Both are the reasons a city ACTUALLY digs a road tunnel; a flat inland grid has neither and gets none.
    ///   * THE TUNNEL, THE PORTAL AND THE TERRAIN HOLE ARE B-18'S. This resolver supplies the alignment (the metro's
    ///     station count, spacing and bored length) and the station box dimensions, and it DECIDES whether the portal
    ///     can be cut -- but the cut, the bore and the underwater running are B-18's linker, unchanged. When the portal
    ///     cannot be cut the metro is refused with the reason rather than built invisibly: the surface must agree with
    ///     what is beneath it. </summary>
    public static class SettlementUndergroundPresence
    {
        /// <summary> How close to its ruling gradient the surface road must be before relief alone forces the road
        /// under. As a <see cref="SuitabilityFactors.SlopeResistance"/> value, which is the square of the grade ratio,
        /// so 0.81 is a surface grade at 90 % of the class limit -- the surface road is within a tenth of the steepest
        /// it may legally be, and a tunnel is the honest alternative rather than a switchback. AUTHORED: it decides how
        /// severe the ground must be to dig, and it is a documented threshold an author can move, not a magic
        /// number. </summary>
        public const double ReliefTunnelSeverity = 0.81;

        /// <summary> The station box this task hands B-18 and the author's prefab: a platform long enough for a
        /// six-car metro train at about twenty metres a car with overruns, a box wide enough for two tracks either side
        /// of an island platform, and a depth typical of a cut-and-cover metro station below the street. AUTHORED real
        /// engineering figures, overridable by the author's station prefab, not measurements of anything -- the metro
        /// dump carries station points, not station dimensions. </summary>
        public const float StationBoxLengthMetres = 140f;
        public const float StationBoxWidthMetres = 22f;
        public const float StationBoxDepthMetres = 18f;

        /// <summary> Answers the three underground questions for one settlement, or refuses the whole call when the
        /// settlement figure itself is unusable.
        ///
        /// <paramref name="identity"/> is the settlement's stable identity, keying the metro draw exactly as surface
        /// rail is keyed; <paramref name="citySeed"/> is the city seed, so a metro is a property of the settlement and
        /// not of an operator's position in a stage list. The physical readings are supplied by the caller because they
        /// are measurements of the ground -- <paramref name="surfaceSlopeFraction"/> the steepest grade a surface road
        /// would climb as a rise/run fraction, <paramref name="roadMaxGradientFraction"/> the road class's own ruling
        /// gradient, <paramref name="waterGapMetres"/> the width of a water crossing on the route and
        /// <paramref name="surfaceMaxCrossingMetres"/> the longest structure the surface network can span, and the three
        /// portal flags what the terrain does where the portal would fall. This resolver reads them; it does not sample
        /// the terrain, which is the builder's and B-18's work. </summary>
        public static SettlementUndergroundReport Resolve(SettlementFitTable table, int orderIndex, string continentKey,
            int population, string identity, ulong citySeed, double surfaceSlopeFraction,
            double roadMaxGradientFraction, double waterGapMetres, double surfaceMaxCrossingMetres,
            bool portalOnWater, bool underwaterCaseAvailable, bool portalGroundValid)
        {
            SettlementUndergroundReport report = new SettlementUndergroundReport();
            report.stationBoxLengthMetres = StationBoxLengthMetres;
            report.stationBoxWidthMetres = StationBoxWidthMetres;
            report.stationBoxDepthMetres = StationBoxDepthMetres;
            report.portalCuttable = true;

            if (population < 0)
            {
                report.refusal = "this settlement has a negative population, which is not a settlement the underground"
                                 + " questions can be asked of";
                return report;
            }
            report.resolved = true;

            // ---- metro presence: the fitted logistic on the underground rows, not a second draw
            report.metro = SettlementRailPresence.Resolve(table, orderIndex, continentKey, population, identity,
                citySeed, SettlementRailPresence.RailMode.Underground);

            // ---- underground road presence: physics, decided the same on every machine and every session
            ResolveUndergroundRoad(ref report, surfaceSlopeFraction, roadMaxGradientFraction, waterGapMetres,
                surfaceMaxCrossingMetres);

            // ---- portal cuttability: only asked of a metro the fit actually served
            if (report.metro.resolved && report.metro.served)
            {
                ResolvePortal(ref report, portalOnWater, underwaterCaseAvailable, portalGroundValid);
                report.metroBuildable = report.portalCuttable;
            }

            return report;
        }

        /// <summary> Fills the underground-road half of the report from the two physical reasons a city digs a road
        /// tunnel. No randomness and no fit: the same ground answers the same way every time, which is what makes "a
        /// flat inland city gets none" a fact rather than a die that mostly rolls low. </summary>
        private static void ResolveUndergroundRoad(ref SettlementUndergroundReport report, double surfaceSlopeFraction,
            double roadMaxGradientFraction, double waterGapMetres, double surfaceMaxCrossingMetres)
        {
            report.reliefSeverity = SuitabilityFactors.SlopeResistance(surfaceSlopeFraction, roadMaxGradientFraction);

            bool gradeKnown = roadMaxGradientFraction > 0.0;
            report.reliefForcesUnderground = gradeKnown && report.reliefSeverity >= ReliefTunnelSeverity;

            bool waterKnown = waterGapMetres > 0.0 && surfaceMaxCrossingMetres > 0.0;
            report.waterGapForcesUnderground = waterKnown && waterGapMetres > surfaceMaxCrossingMetres;

            report.undergroundRoadPresent = report.reliefForcesUnderground || report.waterGapForcesUnderground;

            if (report.reliefForcesUnderground && report.waterGapForcesUnderground)
            {
                report.undergroundRoadReason = "the surface grade is at "
                    + Pct(report.reliefSeverity) + " of the class limit and a "
                    + Metres(waterGapMetres) + " m water gap exceeds the "
                    + Metres(surfaceMaxCrossingMetres) + " m the surface network can span, so both defeat a surface road";
            }
            else if (report.reliefForcesUnderground)
            {
                report.undergroundRoadReason = "the surface grade sits at " + Pct(report.reliefSeverity)
                    + " of the class limit, at or past the point a tunnel replaces a switchback";
            }
            else if (report.waterGapForcesUnderground)
            {
                report.undergroundRoadReason = "a " + Metres(waterGapMetres) + " m water crossing exceeds the "
                    + Metres(surfaceMaxCrossingMetres) + " m the surface network can span, so the road must pass beneath";
            }
            else if (!gradeKnown && waterGapMetres > 0.0)
            {
                report.undergroundRoadReason = "no road gradient limit was supplied, so relief cannot be judged, and the "
                    + Metres(waterGapMetres) + " m water gap is not known to exceed the surface network's reach";
            }
            else
            {
                report.undergroundRoadReason = "the surface grade sits at " + Pct(report.reliefSeverity)
                    + " of the class limit and no water crossing defeats the surface network, so a surface road suffices";
            }
        }

        /// <summary> Fills the portal half of the report. A portal is a hole the terrain has to accept: it cannot be cut
        /// into open water without B-18's underwater case, and it cannot be cut where there is no ground reading at all.
        /// This decides cuttability and states the reason; the cut, the bore and the underwater tube are B-18's linker,
        /// which this resolver never calls. </summary>
        private static void ResolvePortal(ref SettlementUndergroundReport report, bool portalOnWater,
            bool underwaterCaseAvailable, bool portalGroundValid)
        {
            if (!portalGroundValid)
            {
                report.portalCuttable = false;
                report.portalRefusal = "the portal site returned no ground, so there is nothing for the terrain to"
                    + " accept a hole into; the metro is refused here rather than built into empty space";
                return;
            }
            if (portalOnWater && !underwaterCaseAvailable)
            {
                report.portalCuttable = false;
                report.portalRefusal = "the portal falls in open water and B-18's underwater case is not available on"
                    + " this bake, so the portal cannot be cut; the metro is refused rather than buried invisibly";
                return;
            }
            report.portalCuttable = true;
            report.portalRefusal = string.Empty;
        }

        private static string Pct(double fraction)
        {
            return (fraction * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }

        private static string Metres(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
