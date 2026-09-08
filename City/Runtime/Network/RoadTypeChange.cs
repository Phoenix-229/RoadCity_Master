// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using RoadCityMaster;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-103. What a road IS, as data that can be changed on a road that already exists.
    ///
    /// The legacy tool made the kind of road a construction-time decision, not because anyone decided that but because
    /// type-ish information was scattered across a dozen per-road fields with no single owner: an author who laid a
    /// residential street through a valley and later decided it was an arterial had to find and edit all of them
    /// consistently, and got the same road twice only by luck. This file makes the whole type-derived set ONE value —
    /// <see cref="RoadTypeState"/> — so changing class or surface is a rebuild from data rather than a hand-edit of
    /// twelve fields, and so the change can be proved lossless without a scene.
    ///
    /// The merge itself is not new here. <c>Road.ApplyRoadClassDefaults(bool)</c> has done the override-preserving
    /// merge since RM-88, and it still does: it now reads its own fields into a state, calls <see cref="Merge"/>, and
    /// writes the result back, so the rule lives in exactly one place and the round trip a gate measures is the round
    /// trip an author gets. Duplicating the merge as a second "pure" copy beside the live one is how the two drift
    /// until only one of them is true. </summary>
    public static class RoadTypeChange
    {
        #region "Divider facts -- the single owner of both"
        /// <summary> Whether a divider is a physical object in the cross-section rather than paint. Markers are paint;
        /// None is nothing. <c>Road.HasPhysicalMedian()</c> delegates here so the question has one answer. </summary>
        public static bool IsPhysicalDivider(CenterDividerTypeEnum divider)
        {
            return divider != CenterDividerTypeEnum.None && divider != CenterDividerTypeEnum.Markers;
        }


        /// <summary> The real installed base width in metres of a divider — the minimum the cross-section must reserve.
        /// A Caltrans Type K precast concrete barrier ("K-rail"), like the F-shape and single-slope permanent barriers
        /// it stands in for, has a 24 in base, i.e. 0.61 m; a glare screen on top adds height, not base width, so the
        /// blinded variant has the same footprint. Wire rope's posts are too slim to set a median width — such a
        /// barrier is installed in a median already wide enough for it — and paint has no footprint at all. </summary>
        public static float DividerFootprintMetres(CenterDividerTypeEnum divider)
        {
            if (divider == CenterDividerTypeEnum.K_Rail || divider == CenterDividerTypeEnum.KRail_Blinds)
            {
                return 0.61f;
            }
            return 0f;
        }
        #endregion


        #region "The merge -- one rule, shared with the live road"
        /// <summary> Re-derives the type-decided values for <paramref name="incoming"/>, from the state a road is in
        /// now. When <paramref name="preserveOverrides"/> is true a value the author moved away from the OUTGOING
        /// class's default is kept and only values still sitting at that default are refreshed, which is what makes a
        /// class change safe on a road someone has already tuned: their 3.2 m lanes survive, the figures they never
        /// touched follow the new class.
        ///
        /// The surface passes through untouched. Class and surface are two independent axes — an arterial can be
        /// sealed or gravel, and <see cref="RoadClassDefaults"/> has no surface column to merge from — so silently
        /// resetting a chosen surface because the class changed would be inventing a rule nobody wrote down.
        ///
        /// <paramref name="current"/> must already have a split lane model (<c>Road.EnsureLaneModel</c> is called on
        /// the way in); a legacy total is not re-split here, because that rule has an owner and one copy of it. </summary>
        public static RoadTypeState Merge(RoadTypeState current, Road.RoadClassEnum incoming,
            Road.RoadClassEnum applied, bool preserveOverrides)
        {
            RoadClassProfile p = RoadClassDefaults.For(incoming);

            int total = LaneModel.Total(current.lanesForward, current.lanesBackward);
            float laneWidth = current.laneWidth;
            bool shouldersEnabled = current.isShouldersEnabled;
            float shoulderWidth = current.shoulderWidth;
            bool oneWay = current.isOneWay;
            CenterDividerTypeEnum divider = current.centerDivider;
            float speedKPH = current.speedLimitKPH;
            float maxGrade = current.maxGrade;

            if (preserveOverrides)
            {
                RoadClassProfile prev = RoadClassDefaults.For(applied);
                total         = (int)System.Math.Round(RoadClassDefaults.MergeField(prev.LaneCount, total, p.LaneCount));
                laneWidth     = (float)RoadClassDefaults.MergeField(prev.LaneWidthM, laneWidth, p.LaneWidthM);
                shoulderWidth = (float)RoadClassDefaults.MergeField(prev.ShoulderWidthM, shoulderWidth, p.ShoulderWidthM);
                speedKPH      = (float)RoadClassDefaults.MergeField(prev.DesignSpeedKPH, speedKPH, p.DesignSpeedKPH);
                maxGrade      = (float)RoadClassDefaults.MergeField(prev.MaxGrade, maxGrade, p.MaxGrade);
                if (shouldersEnabled == prev.ShoulderEnabled) { shouldersEnabled = p.ShoulderEnabled; }
                if (oneWay == prev.IsOneWay) { oneWay = p.IsOneWay; }
                if (divider == prev.Divider) { divider = p.Divider; }
            }
            else
            {
                total = p.LaneCount;
                laneWidth = (float)p.LaneWidthM;
                shouldersEnabled = p.ShoulderEnabled;
                shoulderWidth = (float)p.ShoulderWidthM;
                oneWay = p.IsOneWay;
                divider = p.Divider;
                speedKPH = (float)p.DesignSpeedKPH;
                maxGrade = (float)p.MaxGrade;
            }

            // The one-way flag decides how a total splits, so the lane model is read only after it has settled.
            int forward, backward;
            LaneModel.SplitTotal(total, oneWay, out forward, out backward);

            // A physical divider has a real footprint the section must at least give it; a width the author already set
            // wider than that is kept, because median width itself is a design choice and not a fixed figure.
            float medianWidth = current.medianWidth;
            float footprint = DividerFootprintMetres(divider);
            if (medianWidth < footprint) { medianWidth = footprint; }
            if (!IsPhysicalDivider(divider) || backward < 1) { medianWidth = 0f; }

            return new RoadTypeState(incoming, forward, backward, oneWay, laneWidth, shouldersEnabled, shoulderWidth,
                divider, medianWidth, speedKPH, current.isMaxGradeEnabled, maxGrade, p.GeneratesMesh, current.surface);
        }
        #endregion


        #region "The five measured values the class table cites"
        /// <summary> The values the class table carries a citation for, and therefore the only ones whose band this
        /// file is entitled to state. Median width is absent on purpose: its floor comes from the divider's own
        /// footprint above, not from the class table, so it has no row to cite.
        ///
        /// RM-110 (B-10) made shoulder width the fifth. It used to be reported by a hand-rolled block below that
        /// stated no band, because the class table cited no shoulder figure; now that every row cites one, the
        /// shoulder goes through the same <see cref="AddMeasured"/> path as the rest and a shoulder widened until it
        /// reads as a lane is flagged as a conflict instead of passing unremarked.
        ///
        /// The three helpers below are if-chains whose LAST line is a fall-through for the last member listed here,
        /// not a default: a member added to this enum must be given its own branch in all three, or it will silently
        /// read as <see cref="ShoulderWidth"/>. </summary>
        private enum Measured
        {
            LaneWidth,
            LaneTotal,
            DesignSpeed,
            MaxGrade,
            ShoulderWidth
        }


        private static double MeasuredValue(RoadClassProfile p, Measured m)
        {
            if (m == Measured.LaneWidth) { return p.LaneWidthM; }
            if (m == Measured.LaneTotal) { return p.LaneCount; }
            if (m == Measured.DesignSpeed) { return p.DesignSpeedKPH; }
            if (m == Measured.MaxGrade) { return p.MaxGrade; }
            return p.ShoulderWidthM;
        }


        private static string MeasuredCitation(RoadClassProfile p, Measured m)
        {
            if (m == Measured.LaneWidth) { return p.LaneWidthCite.Reference; }
            if (m == Measured.LaneTotal) { return p.LaneCountCite.Reference; }
            if (m == Measured.DesignSpeed) { return p.DesignSpeedCite.Reference; }
            if (m == Measured.MaxGrade) { return p.MaxGradeCite.Reference; }
            return p.ShoulderWidthCite.Reference;
        }


        private static string MeasuredFieldName(Measured m)
        {
            if (m == Measured.LaneWidth) { return "laneWidth"; }
            if (m == Measured.LaneTotal) { return "lanes (total)"; }
            if (m == Measured.DesignSpeed) { return "speedLimitKPH"; }
            if (m == Measured.MaxGrade) { return "maxGrade"; }
            return "shoulderWidth";
        }


        /// <summary> The span a value can hold and still be a value some class in the cited table actually uses, with
        /// the citation of the rows at each end. This is READ off the table, never typed in: the low end is whichever
        /// class asks least of that field and the high end whichever asks most, so adding a class widens the span by
        /// itself and no number here can fall out of date. Rows the register does not carry (an incomplete profile)
        /// are skipped, because a zero from a missing row would drag every low end to zero. Returns false when the
        /// table carries no complete row for the field, in which case there is no band to state and none is claimed.
        /// </summary>
        private static bool Envelope(Measured m, out double low, out double high, out string source)
        {
            IReadOnlyList<RoadClassProfile> rows = RoadClassDefaults.All();
            low = 0d; high = 0d; source = "";
            string lowCite = ""; string highCite = "";
            bool any = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].Complete) { continue; }
                double v = MeasuredValue(rows[i], m);
                if (!any) { low = v; high = v; lowCite = MeasuredCitation(rows[i], m); highCite = lowCite; any = true; continue; }
                if (v < low) { low = v; lowCite = MeasuredCitation(rows[i], m); }
                if (v > high) { high = v; highCite = MeasuredCitation(rows[i], m); }
            }
            source = lowCite == highCite ? lowCite : lowCite + " / " + highCite;
            return any;
        }
        #endregion


        #region "What the author changed, and whether keeping it leaves the cited table behind"
        /// <summary> Every value on <paramref name="current"/> that the author moved away from the OUTGOING class's
        /// default, each with what the INCOMING class asks for instead. Nothing is corrected: this is a report, and the
        /// road it describes is left exactly as it is.
        ///
        /// A row is a CONFLICT only when the authored value lies outside the span every class in the cited table works
        /// within — that is a value no road in the table sanctions, which is worth saying out loud. An override inside
        /// the span is just a design decision and is reported as kept. Only the three CHOICES — shoulders on or off,
        /// one-way, and which divider — get no band, because a choice has no span to lie outside of; every dimensioned
        /// field the table cites, shoulder width included since RM-110, is banded from the table itself.
        ///
        /// If the outgoing class is not in the table, its defaults are unknowable, so every value reads as an override.
        /// That over-reports rather than under-reports, which is the right way round for a report an author acts on. </summary>
        public static List<TypeOverride> Overrides(RoadTypeState current, Road.RoadClassEnum applied,
            Road.RoadClassEnum incoming)
        {
            List<TypeOverride> rows = new List<TypeOverride>();
            RoadClassProfile prev = RoadClassDefaults.For(applied);
            RoadClassProfile next = RoadClassDefaults.For(incoming);

            AddMeasured(rows, Measured.LaneWidth, current.laneWidth, prev, next);
            AddMeasured(rows, Measured.LaneTotal, LaneModel.Total(current.lanesForward, current.lanesBackward), prev, next);
            AddMeasured(rows, Measured.DesignSpeed, current.speedLimitKPH, prev, next);
            AddMeasured(rows, Measured.MaxGrade, current.maxGrade, prev, next);
            AddMeasured(rows, Measured.ShoulderWidth, current.shoulderWidth, prev, next);

            if (current.isShouldersEnabled != prev.ShoulderEnabled)
            {
                rows.Add(new TypeOverride("isShouldersEnabled", current.isShouldersEnabled ? "on" : "off",
                    next.ShoulderEnabled ? "on" : "off", "", "", false));
            }
            if (current.isOneWay != prev.IsOneWay)
            {
                rows.Add(new TypeOverride("isOneWay", current.isOneWay ? "on" : "off",
                    next.IsOneWay ? "on" : "off", "", "", false));
            }
            if (current.centerDivider != prev.Divider)
            {
                rows.Add(new TypeOverride("centerDivider", current.centerDivider.ToString(),
                    next.Divider.ToString(), "", "", false));
            }
            return rows;
        }


        private static void AddMeasured(List<TypeOverride> rows, Measured m, double value,
            RoadClassProfile prev, RoadClassProfile next)
        {
            if (!RoadClassDefaults.IsOverride(MeasuredValue(prev, m), value)) { return; }

            CultureInfo ic = CultureInfo.InvariantCulture;
            double low, high; string source;
            bool banded = Envelope(m, out low, out high, out source);
            bool conflict = banded && (value < low || value > high);
            string limit = banded ? low.ToString("R", ic) + " to " + high.ToString("R", ic) : "";

            rows.Add(new TypeOverride(MeasuredFieldName(m), value.ToString("R", ic),
                MeasuredValue(next, m).ToString("R", ic), limit, banded ? source : "", conflict));
        }


        /// <summary> The report as text, one row per line, conflicts first so the thing worth reading is not below the
        /// thing that is merely worth knowing. Empty when the author has overridden nothing. </summary>
        public static string OverrideReport(RoadTypeState current, Road.RoadClassEnum applied,
            Road.RoadClassEnum incoming)
        {
            List<TypeOverride> rows = Overrides(current, applied, incoming);
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].isConflict) { b.Append(rows[i].Message()).Append("\n"); }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].isConflict) { b.Append(rows[i].Message()).Append("\n"); }
            }
            return b.ToString();
        }
        #endregion
    }


    /// <summary> RM-103. The complete set of values a road's TYPE decides, as one value.
    ///
    /// Every field here is one an author could otherwise have had to find and edit by hand after changing their mind
    /// about what a road is. Holding them together is what lets "change this residential street into an arterial" be
    /// a single operation with a single result, and what lets the change be proved reversible: two states are equal
    /// only when every member matches EXACTLY — <c>==</c> on the floats, not a tolerance. A tolerance would let a
    /// lane width come back 3.5000001 m and still call the round trip lossless, and after a dozen such changes the
    /// road is quietly not the road it was. The surface half compares through <c>SurfaceType.ValuesEqual</c>, RM-41's
    /// own comparison, rather than a second copy of it here. </summary>
    public readonly struct RoadTypeState
    {
        #region "Vars"
        public readonly Road.RoadClassEnum roadClass;
        public readonly int lanesForward;
        public readonly int lanesBackward;
        public readonly bool isOneWay;
        public readonly float laneWidth;
        public readonly bool isShouldersEnabled;
        public readonly float shoulderWidth;
        public readonly CenterDividerTypeEnum centerDivider;
        public readonly float medianWidth;
        public readonly float speedLimitKPH;
        public readonly bool isMaxGradeEnabled;
        public readonly float maxGrade;
        public readonly bool isGeneratingRoadMesh;
        public readonly SurfaceTypeClass surface;
        #endregion


        public RoadTypeState(Road.RoadClassEnum roadClass, int lanesForward, int lanesBackward, bool isOneWay,
            float laneWidth, bool isShouldersEnabled, float shoulderWidth, CenterDividerTypeEnum centerDivider,
            float medianWidth, float speedLimitKPH, bool isMaxGradeEnabled, float maxGrade,
            bool isGeneratingRoadMesh, SurfaceTypeClass surface)
        {
            this.roadClass = roadClass;
            this.lanesForward = lanesForward;
            this.lanesBackward = lanesBackward;
            this.isOneWay = isOneWay;
            this.laneWidth = laneWidth;
            this.isShouldersEnabled = isShouldersEnabled;
            this.shoulderWidth = shoulderWidth;
            this.centerDivider = centerDivider;
            this.medianWidth = medianWidth;
            this.speedLimitKPH = speedLimitKPH;
            this.isMaxGradeEnabled = isMaxGradeEnabled;
            this.maxGrade = maxGrade;
            this.isGeneratingRoadMesh = isGeneratingRoadMesh;
            this.surface = surface;
        }


        #region "Equality -- exact, because the round trip is the proof"
        /// <summary> Two states are the same road only if every member is identical. The surface half is decided by
        /// <c>SurfaceType.ValuesEqual</c>, which already answers for two nulls (a road with no surface chosen yet is
        /// equal to another with none chosen: absent is a value, not a mismatch), so there is no second null rule
        /// here to fall out of step with it. </summary>
        public bool Equals(RoadTypeState other)
        {
            return roadClass == other.roadClass
                && lanesForward == other.lanesForward
                && lanesBackward == other.lanesBackward
                && isOneWay == other.isOneWay
                && laneWidth == other.laneWidth
                && isShouldersEnabled == other.isShouldersEnabled
                && shoulderWidth == other.shoulderWidth
                && centerDivider == other.centerDivider
                && medianWidth == other.medianWidth
                && speedLimitKPH == other.speedLimitKPH
                && isMaxGradeEnabled == other.isMaxGradeEnabled
                && maxGrade == other.maxGrade
                && isGeneratingRoadMesh == other.isGeneratingRoadMesh
                && SurfaceType.ValuesEqual(surface, other.surface);
        }


        public override bool Equals(object obj)
        {
            return obj is RoadTypeState && Equals((RoadTypeState)obj);
        }


        /// <summary> The bits of a float, folded to an int, as a hash contribution that means the same thing in every
        /// process. Asking the float for its own hash would very probably do this too, but "probably" is the wrong
        /// word for a value that keys road data: this states the arithmetic instead of inheriting it. The two
        /// normalisations exist so the hash agrees with <see cref="Equals(RoadTypeState)"/>, which compares with
        /// <c>==</c>: negative zero equals positive zero and so must hash alike, and two NaNs are NOT equal, so
        /// nothing is promised about them beyond a constant that keeps them out of the arithmetic. </summary>
        private static int StableBits(float value)
        {
            if (value == 0f) { return 0; }
            if (float.IsNaN(value)) { return 0x7FC00000; }
            long bits = System.BitConverter.DoubleToInt64Bits(value);
            return (int)bits ^ (int)(bits >> 32);
        }


        // determinism-exempt: an integer-field override for equality and dictionary keying, and every float member
        // reaches it through StableBits above rather than through its own hash, so the value is process-stable. The
        // construct the smell test hunts is a generation path keyed on a STRING hash, which is randomised per process.
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (int)roadClass;
            hash = hash * 31 + lanesForward;
            hash = hash * 31 + lanesBackward;
            hash = hash * 31 + (isOneWay ? 1 : 0);
            hash = hash * 31 + StableBits(laneWidth);
            hash = hash * 31 + (isShouldersEnabled ? 1 : 0);
            hash = hash * 31 + StableBits(shoulderWidth);
            hash = hash * 31 + (int)centerDivider;
            hash = hash * 31 + StableBits(medianWidth);
            hash = hash * 31 + StableBits(speedLimitKPH);
            hash = hash * 31 + (isMaxGradeEnabled ? 1 : 0);
            hash = hash * 31 + StableBits(maxGrade);
            hash = hash * 31 + (isGeneratingRoadMesh ? 1 : 0);
            return hash;
        }


        public static bool operator ==(RoadTypeState a, RoadTypeState b) { return a.Equals(b); }
        public static bool operator !=(RoadTypeState a, RoadTypeState b) { return !a.Equals(b); }
        #endregion


        #region "Reading"
        /// <summary> Every member as one line, for a report or a failure message. The floats print round-trippable
        /// ("R"), not rounded to four places: a report that says two values are the same when they differ in the
        /// seventh digit is worse than no report, because the reader then trusts it. Equality is decided by
        /// <see cref="Equals(RoadTypeState)"/>, never by comparing two of these strings — the text exists to be read
        /// by a person, and nothing here depends on its exact shape. </summary>
        public string Signature()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            b.Append("class=").Append(roadClass.ToString());
            b.Append(" lanes=").Append(lanesForward.ToString(ic)).Append("/").Append(lanesBackward.ToString(ic));
            b.Append(" oneWay=").Append(isOneWay ? "1" : "0");
            b.Append(" laneWidth=").Append(laneWidth.ToString("R", ic));
            b.Append(" shoulder=").Append(isShouldersEnabled ? "1" : "0").Append(":").Append(shoulderWidth.ToString("R", ic));
            b.Append(" divider=").Append(centerDivider.ToString());
            b.Append(" median=").Append(medianWidth.ToString("R", ic));
            b.Append(" speedKPH=").Append(speedLimitKPH.ToString("R", ic));
            b.Append(" grade=").Append(isMaxGradeEnabled ? "1" : "0").Append(":").Append(maxGrade.ToString("R", ic));
            b.Append(" mesh=").Append(isGeneratingRoadMesh ? "1" : "0");
            if (surface == null)
            {
                b.Append(" surface=none");
            }
            else
            {
                b.Append(" surface=").Append(surface.surfaceName);
                b.Append(":crown=").Append(surface.crownCrossSlope.ToString("R", ic));
                b.Append(":markings=").Append(surface.emitsLaneMarkings ? "1" : "0");
                b.Append(":paintMargin=").Append(surface.paintMarginMetres.ToString("R", ic));
                b.Append(":ditch=").Append(surface.requiresSideDitch ? "1" : "0");
                b.Append(":materials=").Append(surface.materialSetId);
            }
            return b.ToString();
        }
        #endregion
    }


    /// <summary> RM-103. One value the author moved away from what its class asked for, and whether keeping it puts the
    /// road outside anything the cited table sanctions.
    ///
    /// The values are held as TEXT so one row type covers a lane width, a lane count, a one-way flag and a divider
    /// choice; a numeric row type plus a boolean row type plus an enum row type would be three things to keep in step
    /// for no gain. Nothing here clamps or corrects: an override is a decision the author made, and the tool's job is
    /// to say plainly what it is, what the class expected instead, and where the expectation comes from, so that the
    /// person deciding has both numbers in front of them. A tool that silently pulled 3.2 m up to 3.65 m because the
    /// class changed would be editing the author's design and calling it a default. </summary>
    public readonly struct TypeOverride
    {
        #region "Vars"
        /// <summary> The field's name as it is spelled on the road, so a message can be traced to a control. </summary>
        public readonly string field;
        /// <summary> What the author has it set to. </summary>
        public readonly string authorValue;
        /// <summary> What the class being changed to asks for. </summary>
        public readonly string classDefault;
        /// <summary> The band the value was tested against, empty for a field that has no numeric band (a flag or a
        /// choice can only be one of its listed values, so it cannot leave a band). </summary>
        public readonly string limit;
        /// <summary> Where the expectation comes from — the citation carried by the class table, never a figure typed
        /// in here. </summary>
        public readonly string source;
        /// <summary> True when the authored value lies outside <see cref="limit"/>: no class in the cited table permits
        /// it. An override on its own is not a conflict. </summary>
        public readonly bool isConflict;
        #endregion


        public TypeOverride(string field, string authorValue, string classDefault, string limit, string source,
            bool isConflict)
        {
            this.field = field;
            this.authorValue = authorValue;
            this.classDefault = classDefault;
            this.limit = limit;
            this.source = source;
            this.isConflict = isConflict;
        }


        /// <summary> The row as a sentence an author can act on: both values, then the band and its source when there
        /// is one. </summary>
        public string Message()
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            b.Append(isConflict ? "conflict: " : "kept: ");
            b.Append(field).Append(" is ").Append(authorValue);
            b.Append(", the class asks for ").Append(classDefault);
            if (!string.IsNullOrEmpty(limit))
            {
                b.Append(isConflict ? "; outside the band " : "; the band is ").Append(limit);
            }
            if (!string.IsNullOrEmpty(source)) { b.Append(" (").Append(source).Append(")"); }
            return b.ToString();
        }
    }
}
