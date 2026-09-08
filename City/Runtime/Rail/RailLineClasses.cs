#region "Imports"
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-5. Which KIND of railway a run is.
    ///
    /// D-5 asked the inspector to carry a line class, and a class is the only honest way to offer rail's numbers at
    /// all: a tramway turns on 25 m radii and climbs 40 per mille, a high-speed line needs 4000 m and banks 180 mm,
    /// and no single set of defaults is defensible for both. What differs between them is NUMBERS -- there is one cant
    /// equation (<see cref="RailTrack.EquilibriumCantMillimetres"/>), one transition-length rule
    /// (<see cref="RailTrack.TransitionLengthMetres"/>), one stake (<see cref="AlignmentResolver.ResolveToStandard"/>)
    /// and one cross-section sweep -- so this enum names the four columns those numbers come in and
    /// <see cref="RailLineClasses"/> fills them.
    ///
    /// Four rather than two because the two ends of the range are not a pair of variations on a mainline: a street
    /// tramway and a 300 km/h line differ by two orders of magnitude in radius, and putting a branch line and a
    /// conventional mainline between them is what makes the four a scale rather than two extremes. </summary>
    public enum RailLineClass
    {
        Tramway = 0,
        BranchLine = 1,
        Mainline = 2,
        HighSpeed = 3
    }


    /// <summary> D-5's four authored columns, and the one function that writes them into a run.
    ///
    /// **A preset, not a mode**, for the reason <see cref="PoleRunClasses"/> spells out and this file inherits: the
    /// figures are written INTO the run's own fields and the class is not stored anywhere afterwards. A stored class
    /// beside editable numbers starts lying the moment an author widens a radius, and the tool would then hold two
    /// answers for the design speed with no way to tell which one the cant was computed from.
    ///
    /// Which figures are STANDARD and which are PRACTICE is marked per line, because they are not the same kind of
    /// number. The cant limits, the deficiency limits and the cant rates are EN 13803 / UIC 703 figures. The minimum
    /// radii and ruling gradients are what lines are actually built to, quoted per class. The formation dimensions are
    /// the track bed those lines are laid on. Nothing here is picked to look plausible: a column whose radius and speed
    /// disagree would refuse curves the class exists to allow.
    ///
    /// The one figure that is neither, and the reason a column is worth pressing at all, is what falls out of the
    /// other two: <see cref="Describe"/> ends with the speed the class's own tightest legal curve can be run at, from
    /// <see cref="RailTrack.PermissibleSpeedKmh"/>. A tramway's 25 m radius is runnable at 19 km/h and a high-speed
    /// line's 4000 m at 308 km/h -- and that one number tells an author more about what they just set than the nine
    /// fields it was computed from. </summary>
    public static class RailLineClasses
    {
        /// <summary> The track standard for one column: the radius, gradient and cant limits a run of this kind is
        /// judged by. The gauge is left at 1435 mm in all four -- a metre-gauge or a broad-gauge line is a different
        /// answer to a different question and the author changes two fields for it -- and the figures that move are the
        /// ones that make a tramway a tramway.
        ///
        /// * **Tramway.** 25 m minimum radius and 40 per mille are BOStrab's figures for a German street tramway
        ///   (18 m and 60 per mille are its exceptional cases, which an author sets deliberately rather than gets by
        ///   default). 50 km/h is what a tram in a street is allowed. The cant cap is 60 mm, well under mainline
        ///   practice, because street track is paved across and a carriageway banked 150 mm over a 1.5 m gauge is a
        ///   carriageway a road vehicle crosses on two wheels.
        /// * **Branch line.** 200 m and 25 per mille: a secondary line follows the ground rather than cutting through
        ///   it, which is exactly what a branch is. 100 km/h, and the mainline cant limits, because the stock is the
        ///   same stock.
        /// * **Mainline.** The shipped defaults: 300 m, 15 per mille, 120 km/h, 150 mm of cant and 110 mm of
        ///   deficiency. Conventional mixed-traffic practice.
        /// * **High speed.** 4000 m is the UIC figure for a 300 km/h line and 35 per mille is what an LGV climbs
        ///   (a purpose-built high-speed line can be steeper than a mainline because it carries no freight). The cant
        ///   cap rises to 180 mm, which EN 13803 allows on ballasted high-speed track, while the deficiency limit FALLS
        ///   to 100 mm and both rates to 50 mm/s: at 300 km/h a passenger crosses a transition in a third of the time,
        ///   so the same comfort costs a gentler ramp. The cant gradient tightens to 2 mm/m, 1 in 500, because the
        ///   twist a bogie tolerates does not rise with speed. </summary>
        public static RailTrackClass Track(RailLineClass lineClass)
        {
            RailTrackClass t = RailTrackClass.Default();
            switch (lineClass)
            {
                case RailLineClass.Tramway:
                    t.minimumRadiusMetres = 25f;            // BOStrab: 25 m normal, 18 m exceptional
                    t.maxGradientPerMille = 40f;            // BOStrab: 40 per mille normal, 60 exceptional
                    t.designSpeedKmh = 50f;                 // street running is held to the road's own limit
                    t.maxAppliedCantMillimetres = 60f;      // AUTHORED: paved track has to stay drivable across
                    break;
                case RailLineClass.BranchLine:
                    t.minimumRadiusMetres = 200f;           // secondary-line practice
                    t.maxGradientPerMille = 25f;            // a branch follows the ground
                    t.designSpeedKmh = 100f;
                    break;
                case RailLineClass.Mainline:
                    break;                                  // the shipped defaults ARE the mainline column
                case RailLineClass.HighSpeed:
                    t.minimumRadiusMetres = 4000f;          // UIC 703: 4000 m for 300 km/h
                    t.maxGradientPerMille = 35f;            // LGV practice
                    t.designSpeedKmh = 300f;
                    t.maxAppliedCantMillimetres = 180f;     // EN 13803, ballasted high-speed track
                    t.maxCantDeficiencyMillimetres = 100f;  // EN 13803, V > 200 km/h
                    t.maxCantRateMillimetresPerSecond = 50f;
                    t.maxCantDeficiencyRateMillimetresPerSecond = 50f;
                    t.maxCantGradientMillimetresPerMetre = 2f;   // 1 in 500
                    break;
            }
            return t;
        }


        /// <summary> The formation for one column: the bed the track is laid on, the rail section itself, and the
        /// envelope a structure has to clear. Mainline is the shipped default and the other three move away from it in
        /// the directions the real thing does.
        ///
        /// * **Tramway.** 3 m track centres, the figure a double-track street tramway is laid at, against a mainline's
        ///   4 m. The bed is lighter -- 0.25 m of ballast under a 2.2 m sleeper -- and the earthwork limits drop to 3 m
        ///   because street track does not sit on an embankment. The rail is a GROOVED rail: Ri60 is 180 mm tall on a
        ///   180 mm foot with a 56 mm running head, and its flangeway is the groove in the rail itself, 42 mm wide and
        ///   38 mm deep, rather than a gap left beside a flat-bottom rail. The swept outline is still the flat-bottom
        ///   one at grooved-rail dimensions -- the lip that closes the groove is not modelled -- and the groove an
        ///   author sees is the flangeway the crossing panel leaves, which for a tramway is the whole street.
        ///   The overhead is a 750 V DC trolley wire at 5.5 m, the height a tram line is strung at over a public road,
        ///   with 0.15 m of electrical clearance rather than the 0.27 m a 25 kV line needs.
        /// * **Branch line.** 3.8 m centres and 0.25 m of ballast: an older, lighter formation. Everything else is the
        ///   mainline's, because a branch is built to the same gauge with the same sleepers.
        /// * **High speed.** 4.5 m centres (LGV practice), 0.35 m of ballast under the sleeper and a 0.5 m shoulder --
        ///   both the high-speed figures, and both about a bed that has to hold its geometry under a 300 km/h axle
        ///   rather than about clearance. The contact wire drops to 5.08 m, the nominal high-speed height: a lower wire
        ///   is a stiffer wire, which is what keeps a pantograph in contact at that speed. </summary>
        public static RailFormationClass Formation(RailLineClass lineClass)
        {
            RailFormationClass f = RailFormationClass.Default();
            switch (lineClass)
            {
                case RailLineClass.Tramway:
                    f.trackCentresMetres = 3f;
                    f.sleeperLengthMetres = 2.2f;
                    f.ballastDepthMetres = 0.25f;
                    f.ballastShoulderMetres = 0.3f;
                    f.railHeightMetres = 0.18f;         // Ri60 grooved rail
                    f.railHeadWidthMetres = 0.056f;
                    f.railFootWidthMetres = 0.18f;
                    f.flangewayWidthMetres = 0.042f;    // the groove in the rail
                    f.flangewayDepthMetres = 0.038f;
                    f.loadingGaugeHeightMetres = 4.4f;  // tram vehicle plus its clearance envelope
                    f.contactWireHeightMetres = 5.5f;   // trolley wire over a public road
                    f.electricalClearanceMetres = 0.15f;    // 750 V DC, not 25 kV
                    f.maxEmbankmentMetres = 3f;
                    f.maxCuttingMetres = 3f;
                    break;
                case RailLineClass.BranchLine:
                    f.trackCentresMetres = 3.8f;
                    f.ballastDepthMetres = 0.25f;
                    break;
                case RailLineClass.Mainline:
                    break;
                case RailLineClass.HighSpeed:
                    f.trackCentresMetres = 4.5f;
                    f.ballastDepthMetres = 0.35f;
                    f.ballastShoulderMetres = 0.5f;
                    f.contactWireHeightMetres = 5.08f;
                    break;
            }
            return f;
        }


        /// <summary> B-27 / D-7. The overhead line equipment for one column: how far apart the masts stand, how far the
        /// contact wire is staggered, how tightly the messenger is tensioned and how low the wire may be pulled under a
        /// structure.
        ///
        /// It is a fourth function beside <see cref="Track"/>, <see cref="Formation"/> and <see cref="FitOut"/> rather
        /// than fields added to one of them, and the split follows the same line this file already draws: the track
        /// standard is what a railway is JUDGED by, the formation is what it is BUILT to, the fit-out is what the RUN
        /// is, and this is the equipment strung ABOVE it. A tramway's trolley wire and a 25 kV catenary are different
        /// equipment on the same ballast.
        ///
        /// The two heights that are NOT here -- the nominal contact height and the electrical clearance -- stay on
        /// <see cref="Formation"/>, because <see cref="RailFormationClass.RequiredStructureClearanceMetres"/> is what a
        /// bridge asks and a bridge asks the formation. <see cref="RailOverheadClass"/> argues that at length.
        ///
        /// * **Tramway.** A trolley wire, and almost none of the mainline figures survive. Spans run 25 to 40 m because
        ///   a tramway's masts are street furniture and its curves are 25 m radii; the stagger falls to 0.1 m and the
        ///   pantograph's half sweep to 0.45 m because a tram's head is narrower than a locomotive's. The system height
        ///   drops to 0.3 m -- a trolley wire is often directly suspended with barely a messenger above it, which is
        ///   also why the droppers come every 2 m rather than every 5.4. The floor is 4.0 m, the lowest a wire may hang
        ///   over a public carriageway rather than an electrical limit: at 750 V DC the air gap is 0.15 m and what
        ///   actually sets the height is the lorry underneath.
        /// * **Branch line.** The mainline's equipment. A branch is unwired by default
        ///   (<see cref="FitOut"/> says so) and these figures then go unread, but they are the right ones for the
        ///   author who wires one: a branch electrified is electrified to the same standard.
        /// * **Mainline.** The shipped defaults -- 45 to 65 m spans, 0.2 m stagger, 1.4 m system height, 1 % sag.
        /// * **High speed.** The spans stay long (63 m is LGV practice) but everything about the wire's STIFFNESS
        ///   tightens: the sag halves to 0.5 %, the droppers close up to 4.5 m and the wire gradient tightens to 1 in
        ///   1000, because at 300 km/h a pantograph following a wire that moves vertically leaves it. The constrained
        ///   floor RISES to 5.0 m rather than falling, which reads backwards until the reason is stated: on a
        ///   high-speed line the uplift a pantograph puts into the wire is what governs, and a wire pulled down to
        ///   4.17 m under a bridge is a wire that a pantograph at speed hits the bridge with. A high-speed line is
        ///   built with the clearance instead of borrowing it from the wire. </summary>
        public static RailOverheadClass Overhead(RailLineClass lineClass)
        {
            RailOverheadClass o = RailOverheadClass.Default();
            switch (lineClass)
            {
                case RailLineClass.Tramway:
                    o.maxSpanMetres = 40f;
                    o.minSpanMetres = 25f;
                    o.minCurveSpanMetres = 8f;      // a street corner on a 25 m radius
                    o.staggerMetres = 0.1f;
                    o.pantographHalfSweepMetres = 0.45f;
                    o.systemHeightMetres = 0.3f;    // near-direct suspension, not a simple catenary
                    o.dropperSpacingMetres = 2f;
                    o.minDropperMetres = 0.08f;
                    o.messengerSagRatio = 0.005f;
                    o.constrainedMinimumHeightMetres = 4f;  // road traffic underneath, not the air gap
                    o.maxWireGradient = 0.01f;      // 50 km/h follows a far steeper wire than 300 does
                    o.bracketRiseMetres = 0.2f;
                    break;
                case RailLineClass.BranchLine:
                    break;
                case RailLineClass.Mainline:
                    break;
                case RailLineClass.HighSpeed:
                    o.maxSpanMetres = 63f;
                    o.minSpanMetres = 45f;
                    o.messengerSagRatio = 0.005f;
                    o.dropperSpacingMetres = 4.5f;
                    o.maxWireGradient = 0.001f;             // 1 in 1000: uplift, not comfort
                    o.constrainedMinimumHeightMetres = 5f;  // built with the clearance, not borrowing it
                    break;
            }
            return o;
        }


        /// <summary> The three run-level answers a column implies that belong to neither the track standard nor the
        /// formation: how many tracks the line is, whether it is wired, and how finely the alignment has to be sampled.
        /// A struct for the same reason <c>PoleRunClasses.PoleRunFitOut</c> is one -- they belong to the RUN, and three
        /// separate switch statements over the same enum is how three answers drift apart. </summary>
        public struct RailLineFitOut
        {
            public int trackCount;
            public bool electrified;
            public float sampleSpacingMetres;
        }

        /// <summary> The fit-out for one column.
        ///
        /// The track counts are what the real thing is: a street tramway and a mainline are double, a rural branch is
        /// single. The wiring likewise -- a tramway cannot run without its trolley wire, a branch usually has none, and
        /// a high-speed line is electrified by definition.
        ///
        /// The sample spacing is the one figure here that has to move with the class rather than stay at the shipped
        /// 5 m, and it moves for a geometric reason rather than a stylistic one. Sampling a curve every s metres cuts
        /// it into chords, and a chord's worst departure from the arc is about s^2/(8R). On a mainline's 300 m minimum
        /// a 5 m sample is off by 25/2400 = 0.01 m, which nothing can see. On a tramway's 25 m minimum the same 5 m
        /// sample is off by 25/200 = 0.125 m -- an eighth of a metre of rail cutting the corner, at the one viewing
        /// distance where a tramway is actually looked at. Sampling that curve every 1 m brings it to 1/200 = 0.005 m.
        /// So the tramway samples at 1 m and pays five times the sample count for it, which is affordable precisely
        /// because a street tramway is measured in hundreds of metres where a mainline is measured in tens of
        /// kilometres. </summary>
        public static RailLineFitOut FitOut(RailLineClass lineClass)
        {
            RailLineFitOut f = new RailLineFitOut();
            f.trackCount = 2;
            f.electrified = true;
            f.sampleSpacingMetres = 5f;
            switch (lineClass)
            {
                case RailLineClass.Tramway:
                    f.sampleSpacingMetres = 1f;     // a 5 m chord on a 25 m radius is 0.125 m of visible corner-cutting
                    break;
                case RailLineClass.BranchLine:
                    f.trackCount = 1;
                    f.electrified = false;
                    break;
            }
            return f;
        }


        /// <summary> Write one column into a run, and say what it set.
        ///
        /// What it deliberately does NOT touch is as much of the decision as what it writes. <c>enabled</c> is the
        /// author's switch and a preset that silently turned a run on would be a preset that built something. The
        /// <c>plan</c> is the STAKED CENTRELINE -- the intersection points the author placed by hand -- and a class has
        /// no business moving them; the class decides what radius those points may be resolved to, which is the
        /// <c>track</c> it just wrote. <c>stationStopMetres</c> is where trains stop, which is a decision about a
        /// timetable and not about a kind of railway. The four material slots stay because the author supplied them:
        /// a preset that cleared them would throw away work to set a number.
        ///
        /// <c>trackCount</c> IS written, and that is deliberate rather than an oversight in the list above: a tramway
        /// is double track and a branch line is single, and leaving the count behind would give the author a
        /// single-track high-speed line or a double-track branch and nothing saying the column disagreed.
        ///
        /// The <c>overhead</c> class IS written too, and on an UNWIRED column as well -- which looks like writing a
        /// figure nobody reads and is the point. A branch line comes out unwired, and an author who then ticks
        /// <c>electrified</c> to see what it would cost must get the branch's own equipment rather than whichever
        /// column's figures happened to be there before. What is NOT written is <c>overheadStructures</c>: those are the
        /// bridges and tunnels the author placed, and a preset that cleared them would throw away survey work to set a
        /// span length. </summary>
        public static string Apply(RailAlignmentSettings settings, RailLineClass lineClass)
        {
            if (settings == null) { return "Nothing was set: there is no railway to set it on."; }

            settings.track = Track(lineClass);
            settings.formation = Formation(lineClass);
            settings.overhead = Overhead(lineClass);
            RailLineFitOut fit = FitOut(lineClass);
            settings.trackCount = fit.trackCount;
            settings.electrified = fit.electrified;
            settings.sampleSpacingMetres = fit.sampleSpacingMetres;
            return Describe(lineClass);
        }

        /// <summary> What a column is, in the one sentence the inspector prints after a press. It reads the same three
        /// functions <see cref="Apply"/> writes from, so it cannot describe a figure the run does not carry.
        ///
        /// It ends with the figure an author is really choosing and would otherwise have to solve for: the speed the
        /// class's own tightest legal curve can be run at, from <see cref="RailTrack.PermissibleSpeedKmh"/>. That is
        /// not the design speed and the difference is the point -- a mainline is designed for 120 km/h and its 300 m
        /// minimum radius is good for 81 km/h, so a route taken round the tightest curve the class allows is a route
        /// with a speed restriction on it. Saying so here means the author reads it before staking a line rather than
        /// after. </summary>
        public static string Describe(RailLineClass lineClass)
        {
            RailTrackClass t = Track(lineClass);
            RailFormationClass f = Formation(lineClass);
            RailLineFitOut fit = FitOut(lineClass);
            double atMinimum = RailTrack.PermissibleSpeedKmh(t.minimumRadiusMetres, t);

            string text = Name(lineClass) + ": " + Whole(fit.trackCount)
                          + (fit.trackCount == 1 ? " track" : " tracks") + " at "
                          + Figure(f.trackCentresMetres) + " m centres, curves no tighter than "
                          + Figure(t.minimumRadiusMetres) + " m, gradients no steeper than "
                          + Figure(t.maxGradientPerMille) + " per mille, designed for "
                          + Figure(t.designSpeedKmh) + " km/h with up to " + Figure(t.maxAppliedCantMillimetres)
                          + " mm of cant and " + Figure(t.maxCantDeficiencyMillimetres) + " mm of deficiency";
            RailOverheadClass o = Overhead(lineClass);
            text += fit.electrified
                ? ", wired at " + Figure(f.contactWireHeightMetres) + " m on masts "
                  + Figure(o.minSpanMetres) + " to " + Figure(o.maxSpanMetres) + " m apart, "
                : ", unwired, ";
            text += "sampled every " + Figure(fit.sampleSpacingMetres) + " m. ";
            text += "A curve at that minimum radius can be run at " + Figure(atMinimum) + " km/h.";
            if (!fit.electrified) { return text; }
            // The span a curve at the class minimum permits is the figure an author cannot work out from the fields,
            // and it is the one that says whether the column's own tightest legal curve can be wired at all -- so it
            // is stated here rather than discovered when a run refuses.
            double spanAtMinimum = o.MaxSpanForRadiusMetres(t.minimumRadiusMetres);
            return text + " Through a curve that tight the masts close to " + Figure(spanAtMinimum) + " m.";
        }

        /// <summary> A column's name, spelled once. </summary>
        public static string Name(RailLineClass lineClass)
        {
            switch (lineClass)
            {
                case RailLineClass.Tramway: return "Street tramway";
                case RailLineClass.BranchLine: return "Branch line";
                case RailLineClass.HighSpeed: return "High-speed line";
                default: return "Mainline";
            }
        }

        /// <summary> A measured figure, formatted invariantly so the sentence does not change shape in a comma-decimal
        /// locale. </summary>
        private static string Figure(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Whole(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
