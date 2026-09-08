// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-51's verifiable core. The inspector overhaul's usability wins -- foldout grouping, a unit on
    /// every numeric label, and an inline message that names the limit AND its source rather than a silent clamp --
    /// all rest on one piece of pure data: a per-field descriptor. This file is that data model plus its validation,
    /// with no GUI. The OnInspectorGUI drawers (RoadEditor et al.) CONSUME these descriptors; the visual layout is the
    /// deferred scene half. Keeping the model separate is what makes RM-51 testable headlessly: a gate can assert
    /// every numeric field carries a unit, that an out-of-range value yields a message citing the limit's source, and
    /// that the only product string in the model is the display name "Road & City Master" (C-05). </summary>

    /// <summary> The four foldouts an author reasons in: what the road IS, what it LOOKS like, what it DOES to the
    /// world, and what is GENERATED from it. A flat inspector that mixes a terraform width next to a material slot is
    /// the thing this replaces. Ordering within a group puts every-road fields above once-per-project ones. </summary>
    public enum FieldGroup
    {
        WhatItIs,            // class, surface type, design speed -- the identity of the road
        WhatItLooksLike,     // cross-section, markings, furniture, materials
        WhatItDoesToTheWorld,// terraform widths, cut and fill, structures
        WhatIsGeneratedFromIt// build controls, statistics, reports
    }

    /// <summary> The real-world unit a field is measured in. The label is what appears in the inspector's unit column;
    /// None draws nothing. The MEMBERS were the census's unit vocabulary exactly -- one enum entry per unit the field
    /// census records -- but the token drawn for an author is spelled out where the census abbreviates: a census read by
    /// a maintainer can define `frac` in its legend, and an inspector has no legend for an author to consult.
    ///
    /// C-10 added the last two. The census was taken over the ROAD's serialised surface, which measures lengths, angles
    /// and speeds and never once an area or a rate: the city's node parameters do both -- a lot's target area, and how
    /// many benches stand per kilometre of frontage -- so the vocabulary grew rather than those two fields shipping
    /// unitless.
    ///
    /// B-27's rail work added the two after those. A railway is measured in MILLIMETRES where a road is measured in
    /// metres -- cant is 150 mm and cant deficiency 110 mm, and rounding either to the nearest metre erases the
    /// quantity -- and it states its gradient in PER MILLE rather than per cent, because 15 per mille reads as a
    /// number while 1.5 % reads as a rounding. Both are the real unit of the figure they carry rather than a
    /// conversion of a unit already here, which is why they are members and not a "mm" typed into a label.
    ///
    /// They are appended, never inserted, because a member's position is what a switch reads. </summary>
    public enum FieldUnit
    {
        None, Metres, Degrees, KmPerHour, Percent, Fraction, Multiplier, Index, Reference, SquareMetres,
        PerKilometre, Millimetres, PerMille
    }

    /// <summary> A closed or half-open real-world range with the SOURCE of the limit named, so the inline message can
    /// say "below the minimum radius for 50 km/h in the adopted standard" rather than a bare number. Where no external
    /// standard fixes the value, the source is prefixed AUTHORED (C-15): the number is deliberate, not guessed. </summary>
    public struct FieldRange
    {
        public bool hasMin, hasMax;
        public double min, max;
        public string source;   // where the limit comes from; AUTHORED-prefixed when self-adopted

        public static FieldRange Between(double min, double max, string source)
        {
            return new FieldRange { hasMin = true, hasMax = true, min = min, max = max, source = source };
        }
        public static FieldRange AtLeast(double min, string source)
        {
            return new FieldRange { hasMin = true, hasMax = false, min = min, source = source };
        }
        public static FieldRange None()
        {
            return new FieldRange { hasMin = false, hasMax = false, source = null };
        }
        public bool IsBounded { get { return hasMin || hasMax; } }
    }

    /// <summary> The outcome of validating one value against one field's range: ok, plus a message that (on failure)
    /// names the limit and its source. Never a silent clamp -- the author sees why. </summary>
    public struct ValidationResult
    {
        public bool ok;
        public string message;
        public static ValidationResult Ok() { return new ValidationResult { ok = true, message = null }; }
        public static ValidationResult Fail(string message) { return new ValidationResult { ok = false, message = message }; }
    }

    /// <summary> RM-93's cross-field rules: the two limits on the road section that cannot be written as a
    /// <see cref="FieldRange"/>, because neither is a property of one field.
    ///
    ///   * a backward lane count of 0 is CORRECT on a one-way and WRONG on a two-way, so the limit depends on
    ///     <c>isOneWay</c>;
    ///   * a design speed is too high relative to the geometry the ROAD CLASS was dimensioned for, so the limit
    ///     depends on <c>roadClass</c> and moves when the author changes the class.
    ///
    /// Both delegate: the numbers and their citations are read out of the runtime owners
    /// (<see cref="LaneModel.Unsplit"/>, <see cref="RoadClassDefaults"/>) rather than restated here, so there is
    /// exactly one place where the per-class design speed lives and this file cannot drift away from it. The
    /// drawer shows the message inline at the field; the gate calls the same method to prove it appears. </summary>
    public static class SectionRules
    {
        /// <summary> How far above its class's design speed a road may be driven before the inspector objects. A
        /// posted limit a little over the figure the class was dimensioned for is ordinary -- a jurisdiction raises
        /// a limit without rebuilding the road -- so the check is a tolerance band, not equality. 20 km/h is one
        /// design-speed increment in the AASHTO tables (which step in 10 km/h / 5 mph), i.e. one step of leeway.
        /// AUTHORED (C-15): the increment is real, the choice of one increment is ours. </summary>
        public const double DesignSpeedOverrunToleranceKPH = 20.0;

        /// <summary> Validate the backward lane count against the one-way flag. </summary>
        public static ValidationResult LanesBackward(int lanesBackward, bool isOneWay)
        {
            if (lanesBackward == LaneModel.Unsplit)
            {
                // Not a value an author typed: it is the sentinel that marks a road serialised before the counts
                // were split per direction. EnsureLaneModel replaces it on load, so objecting here would put a
                // red message on every legacy road the moment its inspector was opened.
                return ValidationResult.Ok();
            }
            if (lanesBackward < 0)
            {
                return ValidationResult.Fail(string.Format(CultureInfo.InvariantCulture,
                    "{0} lanes backward is below the minimum 0 (AUTHORED: a direction carries no negative count; "
                    + "{1} is the one legal negative and means unsplit legacy data)",
                    lanesBackward.ToString(CultureInfo.InvariantCulture),
                    LaneModel.Unsplit.ToString(CultureInfo.InvariantCulture)));
            }
            if (!isOneWay && lanesBackward == 0)
            {
                return ValidationResult.Fail(
                    "0 lanes backward is below the minimum 1 for a two-way road (AUTHORED: a two-way carriageway "
                    + "carries at least one lane in each direction; tick One-way to describe a single-direction road)");
            }
            return ValidationResult.Ok();
        }

        /// <summary> Validate an authored speed against the design speed its road class was dimensioned for. </summary>
        public static ValidationResult DesignSpeed(double speedKPH, Road.RoadClassEnum roadClass)
        {
            RoadClassProfile p = RoadClassDefaults.For(roadClass);
            double ceiling = p.DesignSpeedKPH + DesignSpeedOverrunToleranceKPH;
            if (speedKPH <= ceiling) { return ValidationResult.Ok(); }
            return ValidationResult.Fail(string.Format(CultureInfo.InvariantCulture,
                "{0} km/h is above the maximum {1} km/h for road class {2} (its design speed is {3} km/h plus one "
                + "{4} km/h design-speed increment of leeway; {5})",
                speedKPH.ToString(CultureInfo.InvariantCulture),
                ceiling.ToString(CultureInfo.InvariantCulture),
                roadClass.ToString(),
                p.DesignSpeedKPH.ToString(CultureInfo.InvariantCulture),
                DesignSpeedOverrunToleranceKPH.ToString(CultureInfo.InvariantCulture),
                p.DesignSpeedCite.Reference));
        }
    }

    /// <summary> One field's inspector metadata: which foldout it belongs in, its unit, its range-with-source, and the
    /// human label. Load-bearing serialised NAMES are never changed here -- this is presentation, which the
    /// non-breaking contract (RM-50) lets us change freely. </summary>
    public sealed class FieldDescriptor
    {
        public readonly string component;   // e.g. "Road"
        public readonly string field;       // the serialised field name, verbatim -- never renamed here
        public readonly string label;       // author-facing label
        public readonly FieldGroup group;
        public readonly FieldUnit unit;
        public readonly FieldRange range;

        /// <summary> B-5, fault 8. The sentence an author gets on hover: what the value DOES, in the terms of the road
        /// rather than of the code. Not the same information as the range -- the range says what is allowed and where the
        /// limit comes from, which the drawer already shows inline when it is broken -- so a tooltip that restates the
        /// range teaches nothing. Empty on a descriptor built as a test probe; the registry's own rows all carry one, and
        /// RM-102's gate asserts it. </summary>
        public readonly string tooltip;

        public FieldDescriptor(string component, string field, string label,
            FieldGroup group, FieldUnit unit, FieldRange range)
            : this(component, field, label, group, unit, range, null)
        {
        }

        public FieldDescriptor(string component, string field, string label,
            FieldGroup group, FieldUnit unit, FieldRange range, string tooltip)
        {
            this.component = component; this.field = field; this.label = label;
            this.group = group; this.unit = unit; this.range = range;
            this.tooltip = string.IsNullOrEmpty(tooltip) ? "" : tooltip;
        }

        /// <summary> The unit token drawn in the inspector's unit column; empty for None/Reference.
        ///
        /// B-5, fault 6: "frac" was not a unit an author recognises. It is spelled out now. The census keeps its own
        /// abbreviation, because a document with a legend can afford one and an inspector row cannot. </summary>
        public string UnitLabel()
        {
            switch (unit)
            {
                case FieldUnit.Metres: return "m";
                case FieldUnit.Degrees: return "°";
                case FieldUnit.KmPerHour: return "km/h";
                case FieldUnit.Percent: return "%";
                case FieldUnit.Fraction: return "fraction";
                case FieldUnit.Multiplier: return "×";
                case FieldUnit.Index: return "idx";
                case FieldUnit.Reference: return "ref";
                case FieldUnit.SquareMetres: return "m²";
                case FieldUnit.PerKilometre: return "/km";
                case FieldUnit.Millimetres: return "mm";
                case FieldUnit.PerMille: return "‰";
                default: return "";
            }
        }

        /// <summary> True when this field carries a measured numeric unit (so RM-51's gate can require a unit label on
        /// exactly these). Index/Reference/None are non-measured.
        ///
        /// C-10's two city units belong here: an area in m² and a rate per kilometre are both measured quantities an
        /// author reasons about in the world, unlike an index into a catalogue or a reference to an asset. </summary>
        public bool IsNumericMeasured()
        {
            return unit == FieldUnit.Metres || unit == FieldUnit.Degrees || unit == FieldUnit.KmPerHour
                || unit == FieldUnit.Percent || unit == FieldUnit.Fraction || unit == FieldUnit.Multiplier
                || unit == FieldUnit.SquareMetres || unit == FieldUnit.PerKilometre || unit == FieldUnit.Millimetres
                || unit == FieldUnit.PerMille;
        }

        /// <summary> Validate a value against the range. On failure the message names the offending limit and its
        /// source -- the inline text the drawer shows at the field. </summary>
        public ValidationResult Validate(double value)
        {
            string src = string.IsNullOrEmpty(range.source) ? "no source recorded" : range.source;
            string u = UnitLabel();
            string suffix = string.IsNullOrEmpty(u) ? "" : " " + u;
            if (range.hasMin && value < range.min)
            {
                return ValidationResult.Fail(string.Format(CultureInfo.InvariantCulture,
                    "{0}{1} is below the minimum {2}{1} ({3})",
                    value.ToString(CultureInfo.InvariantCulture), suffix, range.min.ToString(CultureInfo.InvariantCulture), src));
            }
            if (range.hasMax && value > range.max)
            {
                return ValidationResult.Fail(string.Format(CultureInfo.InvariantCulture,
                    "{0}{1} is above the maximum {2}{1} ({3})",
                    value.ToString(CultureInfo.InvariantCulture), suffix, range.max.ToString(CultureInfo.InvariantCulture), src));
            }
            return ValidationResult.Ok();
        }
    }
}
#endif
