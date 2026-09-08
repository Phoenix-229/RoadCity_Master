// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> D-4. Which kind of frontage a pole run follows.
    ///
    /// The author drew the distinction as procedural in the city and hand-placed on the main roads, and that is a
    /// distinction of WHO CHOOSES THE RUN, not of what a pole is. There is one pole implementation
    /// (<see cref="PoleMeshKey"/> and its factory), one span solver (<see cref="TransmissionLine.Solve"/>)
    /// and one clearance check (<see cref="TransmissionLine.SpanClearance"/>). What differs between a city street and a
    /// rural highway is NUMBERS, and the numbers are authored: a street run is strung on shorter spans with a secondary
    /// rack and a communication strand under it, a highway run on longer spans with neither.
    ///
    /// So this enum names the three columns those numbers come in, and <see cref="PoleRunClasses"/> fills them. It is
    /// not a stored field on the run: see that class for why. </summary>
    public enum PoleRunClass
    {
        UrbanStreet = 0,
        SuburbanStreet = 1,
        RuralHighway = 2
    }


    /// <summary> D-4's three authored columns, and the one function that writes them into a run.
    ///
    /// **A preset, not a mode.** The figures are written INTO the run's own fields and the class is not stored
    /// anywhere afterwards, deliberately: a stored class beside editable numbers is a field that starts lying the
    /// moment an author nudges a span, and the tool would then have two answers for the span length with no way to tell
    /// which one the wire was strung by. Every number has one home -- the field the inspector draws -- and this
    /// class is the shortcut that fills a sensible set of them in one press.
    ///
    /// Which figures are specification and which are practice is marked per line, because they are not the same kind of
    /// number and a reader who cannot tell them apart cannot argue with either. The NESC clearances and the ANSI O5.1
    /// setting rule are specification and are the same in all three columns -- a road crossing needs 5.6 m whoever is
    /// under it. The spans, the pole lengths, the transformer interval and the offset are PRACTICE: what utilities
    /// actually build, quoted in the ranges RUS distribution construction and the AASHTO Roadside Design Guide state.
    ///
    /// The conductor changes with the span, the one place a class difference is physical rather than a habit: a
    /// 75 m span strung with the 1/0 conductor a 40 m span uses would sag through its clearance, so the highway column
    /// carries 4/0. Both are real product rows and both are named. </summary>
    public static class PoleRunClasses
    {
        /// <summary> The electrical block for one column. The clearances, the tension fraction and the setting rule are
        /// left at their specification values; what changes is the span and the conductor strung over it.
        ///
        /// * Urban and suburban: 1/0 6/1 ACSR (Raven) -- 3.03 N/m, 19.1 kN rated, 10.1 mm overall. The ordinary
        ///   overhead distribution conductor at this voltage, and the one <see cref="DistributionPoleClass"/> already
        ///   defaults to.
        /// * Rural highway: 4/0 6/1 ACSR (Penguin) -- 4.24 N/m, 37.1 kN rated, 14.3 mm overall. Heavier per metre, but
        ///   nearly twice the strength, so at 20 % everyday tension a 75 m span sags 0.40 m where Raven would sag
        ///   0.56 m. </summary>
        public static DistributionPoleClass Electrical(PoleRunClass runClass)
        {
            DistributionPoleClass e = DistributionPoleClass.Default();
            switch (runClass)
            {
                case PoleRunClass.UrbanStreet:
                    e.spanLengthMetres = 40f;   // practice: RUS urban construction, 100 to 150 ft
                    break;
                case PoleRunClass.SuburbanStreet:
                    e.spanLengthMetres = 50f;   // practice: the wider lot frontages between an urban and a rural run
                    break;
                case PoleRunClass.RuralHighway:
                    e.spanLengthMetres = 75f;   // practice: RUS rural construction, 250 to 300 ft
                    e.conductorUnitWeightNewtonsPerMetre = 4.24f;
                    e.conductorRatedStrengthNewtons = 37100f;
                    e.conductorDiameterMetres = 0.0143f;
                    break;
            }
            return e;
        }

        /// <summary> The timber block for one column. Only the LENGTH changes, and it changes to the three standard
        /// ANSI O5.1 lengths a distribution run is ordered in: 35 ft on a city street, 40 ft in the suburbs and on a
        /// rural line. The section (top and butt diameter) is left at the authored class-4 southern-pine taper for all
        /// three, because pole CLASS is a strength choice about wind and ice on the wires, not about the frontage, and
        /// an author who orders a heavier class changes those two figures once for the whole run.
        ///
        /// A 35 ft pole is not a compromise on a street with communication under the supply: set 1.68 m deep it stands
        /// 8.99 m out of the ground, which puts the arm at 8.69 m, the neutral at 8.08 m, the secondary at 7.47 m and a
        /// communication strand 1.02 m under that at 6.45 m -- still clear of the 4.7 m a communication attachment
        /// needs over a road, with the phases 8.53 m up at mid-span against the 5.6 m they need. </summary>
        public static PoleGeometryClass Shape(PoleRunClass runClass)
        {
            PoleGeometryClass s = PoleGeometryClass.Default();
            switch (runClass)
            {
                case PoleRunClass.UrbanStreet:
                    s.poleLengthMetres = 10.67f;    // 35 ft
                    break;
                case PoleRunClass.SuburbanStreet:
                case PoleRunClass.RuralHighway:
                    s.poleLengthMetres = 12.19f;    // 40 ft
                    break;
            }
            return s;
        }

        /// <summary> The five fit-out answers that differ between columns, in one place so <see cref="Apply"/> writes
        /// them and <see cref="Describe"/> reads the same figures. Two switches over one set of numbers is how a
        /// sentence starts describing a run nobody built. </summary>
        public struct PoleRunFitOut
        {
            public float offsetBeyondEdgeMetres;
            public int transformerEveryNPoles;
            public float guyTurnDegrees;
            public bool secondaryRack;
            public bool communicationStrung;
        }

        /// <summary> One column's fit-out. PRACTICE figures, per case:
        ///
        /// * Offset. 1.0 m behind an urban kerb and 1.5 m in the suburbs is where a pole line actually stands: clear
        ///   of the footway, inside the verge. 4.5 m on a rural highway is the low end of the AASHTO Roadside Design
        ///   Guide clear zone; a full clear zone at highway speed on a fill slope is wider, and in practice the
        ///   right-of-way line sets the offset rather than the clear zone, so this is a floor an author raises.
        /// * Transformer interval. Every 6 poles urban (240 m of street at 40 m spans), 8 suburban (400 m), 20 rural
        ///   (1.5 km). It tracks load density and not distance: an urban block of that length carries several dozen
        ///   services and a rural kilometre carries a handful of farms.
        /// * Guy angle. A pole is guyed where the line turns more than this. 10 deg rural against 15 deg urban is not
        ///   a stricter rule, it is the same rule: unbalanced pull is twice the tension times the sine of half the
        ///   turn, and a 75 m span at 4/0 is strung at nearly twice the tension of a 40 m span at 1/0, so a smaller
        ///   turn reaches the same pull.
        /// * Secondary rack and communication. Urban and suburban only. A street has services to drop and a telephone
        ///   or cable plant sharing the pole under NESC Rule 238; a rural primary run has neither, which is why a
        ///   rural pole carries a longer span at the same length -- the height a street spends on the rack and the
        ///   strand beneath it is height a rural span spends on sag. </summary>
        public static PoleRunFitOut FitOut(PoleRunClass runClass)
        {
            PoleRunFitOut f = new PoleRunFitOut();
            f.offsetBeyondEdgeMetres = 1.0f;
            f.transformerEveryNPoles = 6;
            f.guyTurnDegrees = 15f;
            f.secondaryRack = true;
            f.communicationStrung = true;
            switch (runClass)
            {
                case PoleRunClass.SuburbanStreet:
                    f.offsetBeyondEdgeMetres = 1.5f;
                    f.transformerEveryNPoles = 8;
                    break;
                case PoleRunClass.RuralHighway:
                    f.offsetBeyondEdgeMetres = 4.5f;
                    f.transformerEveryNPoles = 20;
                    f.guyTurnDegrees = 10f;
                    f.secondaryRack = false;
                    f.communicationStrung = false;
                    break;
            }
            return f;
        }

        /// <summary> Write one column into a run. Returns the sentence the inspector prints, so an author is told what
        /// changed rather than left to spot it in fifteen fields.
        ///
        /// What this does NOT touch is as deliberate as what it does: <c>enabled</c>, <c>circuit</c>, <c>side</c>, the
        /// start and end stations and the three material slots are the author's own answers about THIS run and no
        /// preset has an opinion about them. A press that silently flipped a run to the other side of the road, or
        /// emptied a material slot the author had filled, would be a press nobody makes twice. </summary>
        public static string Apply(RoadPowerLineSettings settings, PoleRunClass runClass)
        {
            if (settings == null) { return "Nothing was set: there is no run to set it on."; }

            settings.electrical = Electrical(runClass);
            settings.shape = Shape(runClass);
            settings.phaseCount = 3;
            PoleRunFitOut fit = FitOut(runClass);
            settings.offsetBeyondEdgeMetres = fit.offsetBeyondEdgeMetres;
            settings.transformerEveryNPoles = fit.transformerEveryNPoles;
            settings.guyTurnDegrees = fit.guyTurnDegrees;
            settings.secondaryRack = fit.secondaryRack;
            settings.communicationStrung = fit.communicationStrung;
            return Describe(runClass);
        }
        /// <summary> What a column is, in the one sentence the inspector prints after a press. It reads the same three
        /// functions <see cref="Apply"/> writes from, so it cannot describe a figure the run does not carry, and it
        /// ends with the sag the span and the conductor produce -- the number an author is actually deciding when they
        /// pick a column, and the one they would otherwise have to solve to see. </summary>
        public static string Describe(PoleRunClass runClass)
        {
            DistributionPoleClass e = Electrical(runClass);
            PoleGeometryClass s = Shape(runClass);
            PoleRunFitOut f = FitOut(runClass);
            double sag = e.SagRatioAt(e.spanLengthMetres) * e.spanLengthMetres;

            string text = Name(runClass) + ": " + Figure(e.spanLengthMetres) + " m spans on "
                          + Figure(s.poleLengthMetres) + " m poles, set " + Figure(f.offsetBeyondEdgeMetres)
                          + " m beyond the carriageway edge, a transformer every "
                          + Whole(f.transformerEveryNPoles) + " poles, guyed past "
                          + Figure(f.guyTurnDegrees) + " deg of turn";
            text += f.secondaryRack ? ", with a secondary rack" : ", with no secondary rack";
            text += f.communicationStrung
                ? " and a communication strand under it. " : " and no communication strand. ";
            return text + "The conductor sags " + Figure(sag) + " m at that span.";
        }

        /// <summary> A column's name, spelled once. </summary>
        public static string Name(PoleRunClass runClass)
        {
            switch (runClass)
            {
                case PoleRunClass.UrbanStreet: return "Urban street";
                case PoleRunClass.SuburbanStreet: return "Suburban street";
                default: return "Rural highway";
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
