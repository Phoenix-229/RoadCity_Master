#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-12. How the top of a river bank is treated where it passes through a settlement. The treatment
    /// decides the SLOPE the bank is cut to, and a wall is the treatment a frontage too tight for any slope gets. Which
    /// one a bank gets is decided by the width the settlement leaves for it, never by taste -- so this is an authored
    /// PREFERENCE that the width test may override to a wall, not a free choice. </summary>
    public enum BankTreatment
    {
        /// <summary> Grassed earth, cut to the gentlest stable slope (1 vertical : 2 horizontal). The default outside a
        /// dense centre, because a mown earth bank is what a river through a town's edge actually is. </summary>
        GrassedEarth = 0,

        /// <summary> Armoured (riprap / revetment), which holds a STEEPER slope (1 vertical : 1.5 horizontal) because the
        /// facing, not the angle of repose of soil, keeps it. Chosen where the earth slope's run does not fit but a
        /// steeper armoured one still does. </summary>
        Armoured = 1,

        /// <summary> A vertical retaining wall, whose horizontal footprint is its base width rather than a run of slope.
        /// The treatment a city-centre frontage too tight for even an armoured slope gets -- which is exactly what a real
        /// city centre does, and the choice is forced by width, not preferred. </summary>
        RetainingWall = 2
    }


    /// <summary> B-27 / D-12. The cross-section of ONE side of a river bank at one station: how tall the bank is from bed
    /// to top, how far back its top sits from the water's edge, and whether that setback is a slope or a wall. A fitted
    /// answer read the same every bake -- it draws no seed, samples no scene, and follows the river's own depth rather
    /// than a fixed profile, which is why a bank is procedural geometry and not a prefab (a prefab cannot change its
    /// cross-section with the river's depth at every metre). </summary>
    [Serializable]
    public struct BankCrossSection
    {
        public bool resolved;
        public string refusal;
        public BankTreatment treatment;

        /// <summary> Bank height, bed to top: the design water depth plus the freeboard the top stands above the design
        /// water level. The rise the slope or wall must cover. </summary>
        public double bankHeightMetres;

        /// <summary> The top's horizontal setback from the toe at the water's edge: <c>bankHeight * runPerRise</c> for a
        /// slope, the wall base width for a wall. This is the number the frontage must fit. </summary>
        public double horizontalRunMetres;

        /// <summary> Horizontal run per unit rise for a slope treatment (2.0 grassed, 1.5 armoured); zero for a wall,
        /// which does not batter. </summary>
        public double slopeRunPerRise;

        /// <summary> Wall base width, greater than zero only for a retaining wall; zero for a slope. </summary>
        public double wallBaseWidthMetres;

        public string Describe()
        {
            if (!resolved)
            {
                return "river bank: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            string shape;
            if (treatment == BankTreatment.RetainingWall)
            {
                shape = "wall base " + M(wallBaseWidthMetres);
            }
            else
            {
                string slope = slopeRunPerRise.ToString("0.##", CultureInfo.InvariantCulture);
                shape = "slope 1:" + slope + ", run " + M(horizontalRunMetres);
            }
            return treatment + " bank: height " + M(bankHeightMetres) + ", " + shape;
        }

        private static string M(double metres)
        {
            return metres.ToString("0.##", CultureInfo.InvariantCulture) + " m";
        }
    }


    /// <summary> B-27 / D-12. The railing along the top of a bank: how many posts at what spacing, on the side the
    /// pavement is on. Instanced, not a prefab strip -- the count follows the top length, and the post model is content
    /// an author can override. Present on ONE side only, the pavement side; the far bank of a river through a city has no
    /// pavement to rail. </summary>
    [Serializable]
    public struct BankRailing
    {
        public bool present;
        public string refusal;
        public int postCount;
        public double spacingMetres;
        public double topLengthMetres;
        public bool onPavementSide;

        public string Describe()
        {
            if (!present)
            {
                return "bank railing: none -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            string spacing = spacingMetres.ToString("0.##", CultureInfo.InvariantCulture);
            string length = topLengthMetres.ToString("0.##", CultureInfo.InvariantCulture);
            return "bank railing: " + postCount + " posts at " + spacing + " m over " + length + " m of pavement top";
        }
    }


    /// <summary> B-27 / D-12. A crossing of the river: the deck family a span is given, the support stations along it,
    /// and how many of those land where the river runs. It does NOT invent a bridge -- it wraps the shipped
    /// <see cref="BridgeTypes"/>, so a span outside a type's authored range is refused (a 15 m crossing is not given a
    /// cable-stayed deck) and a pier that would stand in water a type has not authored a foundation for is reported
    /// rather than dropped into the river. </summary>
    [Serializable]
    public struct RiverCrossing
    {
        public bool resolved;
        public string refusal;
        public BridgeDeckType deckType;
        public int supportCount;
        public int intermediatePierCount;

        /// <summary> Supports that land inside the wetted width -- allowed only for a type whose foundations may stand in
        /// water. </summary>
        public int piersInWater;

        /// <summary> Supports in water the chosen type has NOT authored a water foundation for. Any above zero refuses
        /// the crossing: the piers cannot stand, and that is the fact the author must see. </summary>
        public int piersRefused;

        public List<double> supportStationsMetres;

        public string Describe()
        {
            if (!resolved)
            {
                return "river crossing: cannot build -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            return deckType + " crossing: " + supportCount + " supports (" + intermediatePierCount
                   + " intermediate), " + piersInWater + " in water";
        }
    }


    /// <summary> B-27 / D-12. The verifiable core of a river through a settlement: the bank's cross-section, the railing
    /// count, and the crossing's supports -- all as figures checkable without a scene. It reads the river's own depth
    /// and width and refuses when they are not a channel; it never mutates a scene, conforms terrain, or draws a seed.
    /// The strip conformed into the terrain, the bank texture, the instanced posts and the generated deck are the
    /// deferred build half; this answers what their dimensions ARE. </summary>
    public static class RiverBank
    {
        // ---- real-world constants, named with the reason each figure is what it is ------------------------------------

        /// <summary> Freeboard the bank top stands above the design water level, in metres -- a minor watercourse's bank
        /// is cut a little above the water it carries so a design flood does not overtop it. </summary>
        public const double FreeboardMetres = 0.6;

        /// <summary> Grassed-earth side slope, horizontal run per unit rise: 1 vertical : 2 horizontal. The gentlest
        /// stable slope for mown earth, and the reason a grassed bank needs the most frontage. </summary>
        public const double GrassedRunPerRise = 2.0;

        /// <summary> Armoured side slope, horizontal run per unit rise: 1 vertical : 1.5 horizontal. Steeper than earth
        /// because the revetment holds it, so it fits a frontage the grassed slope cannot. </summary>
        public const double ArmouredRunPerRise = 1.5;

        /// <summary> Retaining-wall base width as a fraction of its height -- a gravity wall's base is roughly 0.5..0.7 of
        /// its height to stay put; 0.6 is the middle of that range. This is the wall's horizontal footprint. </summary>
        public const double WallBaseWidthPerHeight = 0.6;

        /// <summary> The least base a low retaining wall gets regardless of height, in metres, so a shallow bank's wall
        /// is still buildable. </summary>
        public const double MinWallBaseWidthMetres = 0.4;

        /// <summary> Horizontal run per unit rise for a slope; zero for a wall, which does not batter. </summary>
        public static double RunPerRise(BankTreatment treatment)
        {
            switch (treatment)
            {
                case BankTreatment.GrassedEarth: return GrassedRunPerRise;
                case BankTreatment.Armoured: return ArmouredRunPerRise;
                default: return 0.0;
            }
        }

        /// <summary> Bank height bed-to-top for a design water depth: the depth plus the freeboard. Refuses a
        /// non-positive depth -- that is not a channel to bank. </summary>
        public static bool TryBankHeight(double waterDepthMetres, out double bankHeightMetres, out string refusal)
        {
            bankHeightMetres = 0.0;
            refusal = "";
            if (!(waterDepthMetres > 0.0) || double.IsNaN(waterDepthMetres) || double.IsInfinity(waterDepthMetres))
            {
                refusal = "the water depth is not positive, so there is no channel to bank";
                return false;
            }
            bankHeightMetres = waterDepthMetres + FreeboardMetres;
            return true;
        }

        /// <summary> The cross-section of a chosen SLOPE treatment (grassed or armoured), with no width test. A wall is
        /// resolved through <see cref="ResolveRetainingWall"/>. Refuses a non-slope treatment and a bad depth. </summary>
        public static BankCrossSection ResolveSlopedBank(BankTreatment treatment, double waterDepthMetres)
        {
            BankCrossSection s = new BankCrossSection();
            s.treatment = treatment;
            if (treatment == BankTreatment.RetainingWall)
            {
                s.refusal = "a retaining wall is not a slope; resolve it through ResolveRetainingWall";
                return s;
            }
            double height;
            if (!TryBankHeight(waterDepthMetres, out height, out s.refusal)) { return s; }
            s.resolved = true;
            s.bankHeightMetres = height;
            s.slopeRunPerRise = RunPerRise(treatment);
            s.horizontalRunMetres = height * s.slopeRunPerRise;
            s.wallBaseWidthMetres = 0.0;
            return s;
        }

        /// <summary> The cross-section of a retaining wall for a design water depth: its base width is the gravity-wall
        /// fraction of its height, floored at the minimum. Its footprint is that base, not a run of slope. </summary>
        public static BankCrossSection ResolveRetainingWall(double waterDepthMetres)
        {
            BankCrossSection s = new BankCrossSection();
            s.treatment = BankTreatment.RetainingWall;
            double height;
            if (!TryBankHeight(waterDepthMetres, out height, out s.refusal)) { return s; }
            double baseWidth = Math.Max(MinWallBaseWidthMetres, height * WallBaseWidthPerHeight);
            s.resolved = true;
            s.bankHeightMetres = height;
            s.slopeRunPerRise = 0.0;
            s.wallBaseWidthMetres = baseWidth;
            s.horizontalRunMetres = baseWidth;
            return s;
        }

        /// <summary> Resolve the bank a settlement gets for the frontage it leaves. The choice is made by AVAILABLE
        /// WIDTH, not by taste: the preferred slope is kept if its run fits the frontage; if not, the steeper armoured
        /// slope is tried; and if even that does not fit, a retaining wall -- whose footprint is far smaller -- is what
        /// the frontage gets. A frontage too small for even the wall base is refused, because there is nowhere to stand
        /// the bank at all. </summary>
        public static BankCrossSection ResolveBank(BankTreatment preferred, double waterDepthMetres,
            double availableFrontageMetres)
        {
            double height;
            string refusal;
            if (!TryBankHeight(waterDepthMetres, out height, out refusal))
            {
                BankCrossSection bad = new BankCrossSection();
                bad.treatment = preferred;
                bad.refusal = refusal;
                return bad;
            }

            if (!(availableFrontageMetres > 0.0))
            {
                BankCrossSection bad = new BankCrossSection();
                bad.treatment = preferred;
                bad.refusal = "no frontage was left for the bank, so none of a slope or wall can stand";
                return bad;
            }

            // A preference of "wall" is honoured directly; a slope preference is width-tested and may fall back.
            if (preferred != BankTreatment.RetainingWall)
            {
                BankCrossSection slope = ResolveSlopedBank(preferred, waterDepthMetres);
                if (slope.resolved && slope.horizontalRunMetres <= availableFrontageMetres + 1e-9) { return slope; }

                if (preferred != BankTreatment.Armoured)
                {
                    BankCrossSection armoured = ResolveSlopedBank(BankTreatment.Armoured, waterDepthMetres);
                    if (armoured.resolved && armoured.horizontalRunMetres <= availableFrontageMetres + 1e-9)
                    {
                        return armoured;
                    }
                }
            }

            BankCrossSection wall = ResolveRetainingWall(waterDepthMetres);
            if (wall.resolved && wall.horizontalRunMetres <= availableFrontageMetres + 1e-9) { return wall; }

            BankCrossSection none = new BankCrossSection();
            none.treatment = BankTreatment.RetainingWall;
            string frontageText = availableFrontageMetres.ToString("0.##", CultureInfo.InvariantCulture);
            string baseText = wall.wallBaseWidthMetres.ToString("0.##", CultureInfo.InvariantCulture);
            none.refusal = "the frontage " + frontageText + " m does not fit even a " + baseText + " m wall base";
            return none;
        }

        /// <summary> The railing along the top of a bank. Present on the pavement side only, instanced at the given
        /// spacing: the posts run the length of the top, both ends included, so a 100 m top at 2 m spacing gets 51
        /// posts. Refuses when there is no pavement on this side, a non-positive length, or a non-positive spacing --
        /// none of which is a railing to place. </summary>
        public static BankRailing ResolveRailing(double topLengthMetres, double spacingMetres, bool pavementOnThisSide)
        {
            BankRailing r = new BankRailing();
            r.spacingMetres = spacingMetres;
            r.topLengthMetres = topLengthMetres;
            r.onPavementSide = pavementOnThisSide;
            if (!pavementOnThisSide)
            {
                r.refusal = "this side carries no pavement, so it takes no railing";
                return r;
            }
            if (!(topLengthMetres > 0.0))
            {
                r.refusal = "the bank top has no length to rail";
                return r;
            }
            if (!(spacingMetres > 0.0))
            {
                r.refusal = "the post spacing is not positive";
                return r;
            }
            r.present = true;
            r.postCount = (int)Math.Floor(topLengthMetres / spacingMetres) + 1;
            return r;
        }

        /// <summary> Select the SIMPLEST shipped bridge type whose authored span range contains the span: beam first,
        /// then arch, then suspension. This is selection by span with the real ranges the types carry, so a short span
        /// gets a beam and a very long one a suspension -- and a span no shipped type covers returns an unspecified
        /// sheet, which the crossing refuses rather than forcing a type onto a span it was not authored for. </summary>
        public static BridgeTypeSpec SelectTypeForSpan(double spanMetres)
        {
            BridgeTypeSpec beam = BridgeTypeSpec.Beam();
            if (BridgeTypes.SpanWithinType(spanMetres, beam)) { return beam; }
            BridgeTypeSpec arch = BridgeTypeSpec.Arch();
            if (BridgeTypes.SpanWithinType(spanMetres, arch)) { return arch; }
            BridgeTypeSpec suspension = BridgeTypeSpec.Suspension();
            if (BridgeTypes.SpanWithinType(spanMetres, suspension)) { return suspension; }
            return BridgeTypeSpec.Unspecified();
        }

        /// <summary> Resolve a crossing of the river with a chosen bridge type. Validates the type, refuses a span
        /// outside its authored range, takes the support stations the type requires, and tests each against the wetted
        /// width the river occupies. A support inside the water is allowed only for a type whose foundations may stand in
        /// water; any support in water for a type that may not stands refused, and the crossing reports the count rather
        /// than dropping a pier into the river. The wetted band is centred at <paramref name="wettedCentreMetres"/> along
        /// the span and is <paramref name="wettedWidthMetres"/> wide; a non-positive width means the span clears the
        /// water and no support can land in it. </summary>
        public static RiverCrossing ResolveCrossing(double spanMetres, BridgeTypeSpec spec,
            double wettedCentreMetres, double wettedWidthMetres)
        {
            RiverCrossing c = new RiverCrossing();
            c.deckType = spec.deckType;
            c.supportStationsMetres = new List<double>();

            BridgeTypes.SpecValidation v = BridgeTypes.ValidateSpec(spec);
            if (!v.accepted) { c.refusal = v.message; return c; }

            if (!(spanMetres > 0.0))
            {
                c.refusal = "the crossing span is not positive";
                return c;
            }
            if (!BridgeTypes.SpanWithinType(spanMetres, spec))
            {
                c.refusal = "the span " + spanMetres.ToString("0.##", CultureInfo.InvariantCulture)
                            + " m is outside the " + spec.deckType + " type's authored range "
                            + spec.minSpanMetres.ToString("0.##", CultureInfo.InvariantCulture) + ".."
                            + spec.maxSpanMetres.ToString("0.##", CultureInfo.InvariantCulture) + " m";
                return c;
            }

            List<double> stations = BridgeTypes.SupportStationsMetres(spanMetres, spec);
            c.supportStationsMetres = stations;
            c.supportCount = stations.Count;
            c.intermediatePierCount = BridgeTypes.IntermediatePierCount(spanMetres, spec);

            double half = wettedWidthMetres > 0.0 ? wettedWidthMetres * 0.5 : 0.0;
            int inWater = 0;
            int refused = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                bool landsInWater = half > 0.0 && Math.Abs(stations[i] - wettedCentreMetres) < half - 1e-9;
                if (!landsInWater) { continue; }
                inWater++;
                if (!BridgeTypes.SupportAllowedAt(true, spec)) { refused++; }
            }
            c.piersInWater = inWater;
            c.piersRefused = refused;

            if (refused > 0)
            {
                c.refusal = refused + (refused == 1 ? " support lands" : " supports land") + " in the river, which the "
                            + spec.deckType + " type has no water foundation for";
                return c;
            }

            c.resolved = true;
            return c;
        }
    }
}
