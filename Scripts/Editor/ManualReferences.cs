#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-65's three registers -- the machine-readable half of the tool's own manual.
    ///
    /// The manual's three reference sections are generated from here rather than typed, because RM-65's gate is about
    /// DRIFT: a manual that names a field the inspector does not have, or a unit the inspector does not draw, is worse
    /// than no manual. So the units-and-ranges section is not a transcription of <see cref="InspectorFieldRegistry"/> --
    /// it IS <see cref="InspectorFieldRegistry"/>, walked. A field cannot drift out of the manual without leaving the
    /// registry, and leaving the registry is what RM-51's own gate refuses.
    ///
    /// The refusal catalogue and the cited-standards table are registers in their own right: the refusal wording lives
    /// here so the drawer, the status panel (RM-52) and the manual all read one string, and the standards table records
    /// what edition each number was taken from -- including, honestly, the rows where the project has not yet adopted
    /// an edition, which are marked as such rather than filled with a plausible one (C-15). </summary>

    /// <summary> One refusal an author can hit, in the module's fail-loudly style: the exact wording, why the tool
    /// refuses instead of clamping, and the two or three ways out.
    ///
    /// Named `ManualRefusal` rather than `RefusalEntry` because `RoadCityMaster.RefusalEntry` already exists in the
    /// runtime assembly (`BuildStatusModel.cs`), where it is the serialised record of a refusal a build actually hit --
    /// object, station, reason. Two same-named types in one namespace do not clash at compile time inside the editor
    /// assembly: the local one silently wins, and `StatusPanelGate` was left calling this four-string constructor with
    /// the runtime type's arguments. One catalogue entry, one build record, two names. </summary>
    public sealed class ManualRefusal
    {
        public readonly string id;        // stable upper-case token; the status panel groups by it
        public readonly string message;   // the wording the author sees, at the field or in the panel
        public readonly string why;       // why a refusal and not a silent clamp; names the owning task
        private readonly string[] _resolutions;

        public ManualRefusal(string id, string message, string why, string[] resolutions)
        {
            this.id = id; this.message = message; this.why = why;
            _resolutions = resolutions == null ? new string[0] : resolutions;
        }

        /// <summary> A copy, so a caller cannot edit the catalogue through the array it was handed. </summary>
        public IList<string> Resolutions() { return new List<string>(_resolutions); }

        public int ResolutionCount { get { return _resolutions.Length; } }

        /// <summary> The resolutions as one numbered sentence run, for a table cell. </summary>
        public string ResolutionText()
        {
            string s = "";
            for (int i = 0; i < _resolutions.Length; i++)
            {
                if (i > 0) { s += " "; }
                s += (i + 1).ToString(CultureInfo.InvariantCulture) + ") " + _resolutions[i];
            }
            return s;
        }
    }

    /// <summary> One row of the cited-standards table: the feature, the quantity taken from the source, the source, the
    /// edition that quantity was read from, and the task that cites it. Where § 10 says "the standard the project
    /// adopts" and the adoption decision is still open, the edition is one of the two PENDING tokens -- never a guess,
    /// because a wrong edition is the one documentation defect an auditor can catch and nobody else can. </summary>
    public sealed class CitedStandard
    {
        /// <summary> § 10 defers the geometric design standard to Q-13. Until Q-13 is answered, every quantity drawn
        /// from it is authored and says so. </summary>
        public const string PendingGeometric = "NOT ADOPTED -- Q-13 (geometric design standard) is open";

        /// <summary> § 10 defers the marking standard to Q-14. </summary>
        public const string PendingMarking = "NOT ADOPTED -- Q-14 (marking standard) is open";

        public readonly string task;      // the RM-nn that carries the cite line
        public readonly string feature;   // what the author is doing when the number is used
        public readonly string quantity;  // the specific quantity or relation taken from the source
        public readonly string source;    // the document
        public readonly string edition;   // the edition used, or one of the PENDING tokens

        public CitedStandard(string task, string feature, string quantity, string source, string edition)
        {
            this.task = task; this.feature = feature; this.quantity = quantity;
            this.source = source; this.edition = edition;
        }

        /// <summary> True when a real edition is recorded, false when the project has not adopted one yet. </summary>
        public bool IsAdopted
        {
            get { return edition != PendingGeometric && edition != PendingMarking; }
        }
    }

    /// <summary> The three registers the manual's reference sections are generated from. </summary>
    public static class ManualReferences
    {
        // ---------------------------------------------------------------- fields, units and ranges

        /// <summary> RM-51's registry, walked. Not a copy of it: the same descriptors, so a field cannot be documented
        /// under a name or unit the inspector does not use. </summary>
        public static IList<FieldDescriptor> Fields()
        {
            return InspectorFieldRegistry.All();
        }

        /// <summary> The author-facing name of a foldout group -- RM-64's four-group vocabulary in prose. </summary>
        public static string GroupText(FieldGroup group)
        {
            switch (group)
            {
                case FieldGroup.WhatItIs: return "What it is";
                case FieldGroup.WhatItLooksLike: return "What it looks like";
                case FieldGroup.WhatItDoesToTheWorld: return "What it does to the world";
                default: return "What is generated from it";
            }
        }

        /// <summary> The unit as the manual prints it; a dimensionless field says so rather than leaving a blank cell,
        /// because a blank cell reads as an oversight. </summary>
        public static string UnitText(FieldDescriptor d)
        {
            string u = d.UnitLabel();
            return u.Length == 0 ? "dimensionless" : u;
        }

        /// <summary> The valid range in the field's own unit, invariant-formatted (C-06). </summary>
        public static string RangeText(FieldDescriptor d)
        {
            string u = d.UnitLabel();
            string suffix = u.Length == 0 ? "" : " " + u;
            if (d.range.hasMin && d.range.hasMax)
            {
                return d.range.min.ToString(CultureInfo.InvariantCulture) + " to "
                    + d.range.max.ToString(CultureInfo.InvariantCulture) + suffix;
            }
            if (d.range.hasMin) { return "at least " + d.range.min.ToString(CultureInfo.InvariantCulture) + suffix; }
            if (d.range.hasMax) { return "at most " + d.range.max.ToString(CultureInfo.InvariantCulture) + suffix; }
            return "unbounded";
        }

        /// <summary> Where the limit comes from, verbatim from the descriptor -- AUTHORED-prefixed when the project set
        /// it rather than a standard. </summary>
        public static string SourceText(FieldDescriptor d)
        {
            return string.IsNullOrEmpty(d.range.source) ? "no source recorded" : d.range.source;
        }

        /// <summary> The units-and-ranges reference, one row per shipped field. </summary>
        public static string FieldsTable()
        {
            string t = "| Component | Field | Author sees | Group | Unit | Valid range | Where the limit comes from |\n"
                + "| --- | --- | --- | --- | --- | --- | --- |";
            foreach (FieldDescriptor d in Fields())
            {
                t += "\n| `" + d.component + "` | `" + d.field + "` | " + d.label + " | " + GroupText(d.group)
                    + " | " + UnitText(d) + " | " + RangeText(d) + " | " + SourceText(d) + " |";
            }
            return t;
        }

        // ---------------------------------------------------------------- refusals

        private static List<ManualRefusal> _refusals;

        /// <summary> Every refusal this module introduces, in id order. One string per refusal, read by the drawer, by
        /// RM-52's status panel and by the manual, so the author sees the same wording in all three places. </summary>
        public static IList<ManualRefusal> Refusals()
        {
            if (_refusals != null) { return new List<ManualRefusal>(_refusals); }
            List<ManualRefusal> r = new List<ManualRefusal>();

            r.Add(new ManualRefusal("BARRIER-RUN-WITHOUT-TERMINAL",
                "This median barrier run has no terminal at its start. A barrier that stops in open ground is more "
                + "dangerous than no barrier, so the run was not built.",
                "RM-60 requires a proper end treatment on every barrier run and RM-76 owns run termination in general. "
                + "A blunt end is a struck-object hazard, which is why this refuses rather than trimming the run.",
                new string[] {
                    "Extend the run to a point where a terminal fits.",
                    "Choose an end treatment the run has length for (crash cushion, flared terminal, buried end).",
                    "Remove the barrier from this median segment and use a trough or planting instead."
                }));

            r.Add(new ManualRefusal("GRADE-ABOVE-MAXIMUM",
                "The profile between these two stations needs a grade above the maximum for this road class. "
                + "The vertical alignment was not solved.",
                "RM-95 solves grades and vertical curves against the adopted standard's per-class maximum, and "
                + "`Road.maxGrade` carries the same ceiling on the inspector. A road steeper than its class cannot be "
                + "driven at the class's design speed, so the tool refuses instead of quietly exceeding it.",
                new string[] {
                    "Add horizontal distance between the two elevations.",
                    "Lower the design speed, and with it the class, for this length of road.",
                    "Cut or fill the terrain so the two ends are closer in height."
                }));

            r.Add(new ManualRefusal("GRADE-SEPARATION-NO-APPROACH",
                "These two roads cross without touching, but there is not enough clear road either side of the "
                + "crossing to raise one over the other. The crossing was left as it is and flagged.",
                "RM-35 measures the real available approach length on each side before it separates a crossing, "
                + "because the raise needs a vertical curve at no more than the class's maximum gradient. Building the "
                + "bridge anyway would put a kink in the road at the abutment.",
                new string[] {
                    "Move one of the roads so the crossing has more clear approach.",
                    "Separate the crossing by hand with a bridge whose approach you control.",
                    "Turn the crossing into an at-grade junction instead."
                }));

            r.Add(new ManualRefusal("INTERSECTION-SIGHT-TRIANGLE-BLOCKED",
                "The sight triangle on this approach is obstructed. The intersection was built and the obstruction "
                + "reported rather than silently accepted.",
                "RM-97 solves the intersection sight triangle from the adopted standard's leg relations. The tool "
                + "reports rather than moves an author's object, because what is inside the triangle is the author's "
                + "content and only the author knows what may be removed.",
                new string[] {
                    "Move or remove the object inside the triangle.",
                    "Lower the design speed on the approach, which shortens the legs.",
                    "Accept the obstruction deliberately and record it, so the report stops being a surprise."
                }));

            r.Add(new ManualRefusal("LOT-NOT-BUILDABLE",
                "This lot fails the buildable-land test on slope, water or an existing footprint. No building was "
                + "placed on it.",
                "RM-11 tests every lot before RM-12 places anything, so a building never ends up part-submerged or "
                + "cantilevered off a bank. A rejected lot stays a lot -- it is not deleted -- so the author can see "
                + "which land the city declined.",
                new string[] {
                    "Reshape the lot, or let RM-11 reshape it, until it clears the slope threshold.",
                    "Flatten the ground under it deliberately with a building pad (RM-22).",
                    "Leave it as open space, which is what a real subdivision does with unbuildable land."
                }));

            r.Add(new ManualRefusal("PREFAB-CATEGORY-EMPTY",
                "Nothing is assigned to this content category, so nothing was placed. The tool does not substitute "
                + "content you did not choose.",
                "RM-55 is the module's fail-closed rule: an empty category produces a refusal and a count, never a "
                + "stand-in shape and never the nearest category's content. A stand-in looks like a bug in your art, "
                + "and you find it late.",
                new string[] {
                    "Assign prefabs to the category from RM-77's list of what the system needs.",
                    "Turn the feature off, so the category is not asked for.",
                    "Point the category at another category's content deliberately, which is a choice and is recorded."
                }));

            r.Add(new ManualRefusal("PROVIDER-REQUIRED",
                "This feature needs a terrain provider connected. Connect one, or use the standalone equivalent named "
                + "in the availability table.",
                "RM-58 keeps the standalone path fully working and states, in one sentence per feature, what a feature "
                + "needs. Nothing that needs a provider is allowed to silently do nothing -- that is the failure mode "
                + "the availability table exists to prevent.",
                new string[] {
                    "Connect the host's terrain provider to the road system.",
                    "Use the standalone equivalent the availability table names for this feature.",
                    "Leave the feature off; the rest of the tool is unaffected."
                }));

            r.Add(new ManualRefusal("RADIUS-BELOW-MINIMUM",
                "This curve's radius is below the minimum for its design speed in the adopted standard. The alignment "
                + "was not solved through it.",
                "RM-75 solves horizontal alignment from the minimum-radius relation e + f = V^2 / (127 x R), and RM-61 "
                + "applies the same rule to a roundabout's entry geometry. A curve tighter than the minimum cannot be "
                + "driven at the speed the class advertises, so the tool refuses instead of banking it further.",
                new string[] {
                    "Increase the radius of the curve.",
                    "Lower the design speed, or the road class, for this length of road.",
                    "Raise the superelevation rate, up to the maximum the standard allows for the class."
                }));

            r.Add(new ManualRefusal("SUPERELEVATION-ABOVE-MAXIMUM",
                "Banking this curve for its design speed needs a cross-slope above the authored maximum rate. The "
                + "curve was not banked.",
                "RM-40 derives the rate from e + f = V^2 / (127 x R) with the side-friction values of the same edition, "
                + "and will not exceed the maximum rate the standard sets for the road's environment. Over-banking a "
                + "curve is unsafe for slow and stopped vehicles, which is why the ceiling is not negotiable.",
                new string[] {
                    "Increase the radius, which lowers the rate the relation asks for.",
                    "Lower the design speed for this length of road.",
                    "Accept the maximum rate and the lower cornering speed that comes with it."
                }));

            r.Add(new ManualRefusal("TAPER-NO-APPROACH-LENGTH",
                "The lane count changes here, but there is not enough road before the junction to run a merge taper. "
                + "The taper was shortened to the available length and the junction flagged.",
                "RM-27 passes the measured available approach length in rather than assuming one, and reports the ideal "
                + "against the achieved length. A taper compressed into no distance is a lane that ends abruptly, so "
                + "the shortening is reported rather than hidden.",
                new string[] {
                    "Move the lane-count change further back from the junction.",
                    "Reduce the difference in lane count across the change.",
                    "Accept the shortened taper knowingly; the junction stays flagged so it is not forgotten."
                }));

            r.Add(new ManualRefusal("TERMINUS-UNTREATED",
                "This road ends in mid-air with no terminus treatment. No end was built.",
                "RM-26 requires a road to end in something real -- a turning head, a stub with a barrier, a fade into "
                + "an unsealed track. A carriageway that simply stops is the single most visible defect in a generated "
                + "network, and it is cheap to refuse and expensive to notice later.",
                new string[] {
                    "Choose a terminus treatment for the end node.",
                    "Connect the end to another road, so it is a junction rather than an end.",
                    "Extend the road until it leaves the built area, where an untreated end is not visible."
                }));

            r.Add(new ManualRefusal("TUNNEL-COVER-INSUFFICIENT",
                "There is not enough ground above this tunnel to make a cavity here. No opening was requested and no "
                + "trench was cut instead.",
                "RM-37 asks the provider for a cavity through the ground rather than cutting a trench, and a cavity "
                + "with too little cover has no roof. Cutting a trench as a fallback is what the old behaviour did, "
                + "and it is what this refusal exists to stop.",
                new string[] {
                    "Lower the road, so there is more ground above it.",
                    "Raise the terrain over the alignment deliberately.",
                    "Build the crossing as a cutting, or as a bridge over the obstacle, instead of a tunnel."
                }));

            _refusals = r;
            return new List<ManualRefusal>(_refusals);
        }

        /// <summary> The refusals reference, one row per refusal. </summary>
        public static string RefusalsTable()
        {
            string t = "| Refusal | What the author sees | Why the tool refuses | How to resolve it |\n"
                + "| --- | --- | --- | --- |";
            foreach (ManualRefusal e in Refusals())
            {
                t += "\n| `" + e.id + "` | " + e.message + " | " + e.why + " | " + e.ResolutionText() + " |";
            }
            return t;
        }

        // ---------------------------------------------------------------- cited standards

        /// <summary> The geometric design standard § 10 defers to Q-13. Named as a CANDIDATE only: no quantity in the
        /// shipped tool is read from it until Q-13 adopts it, and every row that points here carries the pending token
        /// in its edition column so an auditor can see the difference. </summary>
        public const string GeometricCandidate =
            "Candidate: AASHTO, A Policy on Geometric Design of Highways and Streets";

        private static List<CitedStandard> _standards;

        /// <summary> One row per **Cite** line in § 10, in task order. Twelve lines, twelve rows: the point of the table
        /// is that a reader who was not in the design conversations can check the module's "nothing invented" rule
        /// against the sources, including where a source has not been fixed yet. </summary>
        public static IList<CitedStandard> Standards()
        {
            if (_standards != null) { return new List<CitedStandard>(_standards); }
            List<CitedStandard> s = new List<CitedStandard>();

            s.Add(new CitedStandard("RM-28", "Roundabout as a junction type",
                "Every geometric quantity: inscribed circle diameter, entry and exit radii, splitter island, "
                + "circulatory width",
                "NCHRP Report 672, Roundabouts: An Informational Guide (AASHTO-aligned)",
                "NCHRP Report 672 (2010), the second edition of the informational guide"));

            s.Add(new CitedStandard("RM-30", "Culverts and roadside ditches",
                "Culvert barrel sizing; Manning's equation for open-channel flow in the ditch",
                "FHWA Hydraulic Design Series No. 5, Hydraulic Design of Highway Culverts (HDS-5)",
                "HDS-5 third edition, FHWA-HIF-12-026 (2012)"));

            s.Add(new CitedStandard("RM-32", "Pavement markings",
                "Dash lengths, gap ratios and colours",
                "The marking standard the author selects: MUTCD (US and Canada), UK Traffic Signs Manual Chapter 5, "
                + "or the Vienna Convention family",
                CitedStandard.PendingMarking));

            s.Add(new CitedStandard("RM-40", "Superelevation through curves",
                "e + f = V^2 / (127 x R) with e the cross-slope as a decimal, f the side-friction factor, V in km/h, "
                + "R in metres; and the side-friction values of the same edition",
                GeometricCandidate, CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-41", "Unsealed roads as a surface type",
                "The crown figure for an unsealed road, which is meaningfully steeper than a sealed road's crown",
                "The low-volume or gravel-road manual the project adopts", CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-43", "Transport hubs: the airport case",
                "Approach and obstacle-limitation surfaces, as published slope-and-dimension surfaces",
                "The aerodrome standard the project adopts (candidate: ICAO Annex 14, Volume I, Aerodrome Design and "
                + "Operations)", CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-61", "Shape-flexible roundabout",
                "Inscribed circle diameter and entry geometry, re-solved for a non-circular circulatory carriageway",
                "The roundabout design guide the project adopts (NCHRP Report 672, as RM-28)",
                "NCHRP Report 672 (2010), the second edition of the informational guide"));

            s.Add(new CitedStandard("RM-74", "Suitability and corridor map",
                "Weighted-overlay suitability analysis, least-cost path and cost-distance corridor analysis, and the "
                + "corridor-selection stage of highway route location",
                "Established GIS and landscape-planning methods (a method, not a numbered standard); the "
                + "route-location stage belongs to the adopted geometric design standard",
                CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-75", "Horizontal alignment",
                "The tangent-curve-transition model, and the minimum-radius relation e + f = V^2 / (127 x R) solved "
                + "for R", GeometricCandidate, CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-95", "Vertical alignment",
                "L = K x A; the per-design-speed K values for crests and sags; the per-class maximum grades; and "
                + "SSD = 0.278 x V x t + V^2 / (254 x (f +/- G))",
                GeometricCandidate, CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-96", "Carriageway cross-section",
                "Normal cross-fall by surface type, the maximum relative gradient governing superelevation runoff, "
                + "the runoff and runout length relations, and the curve-widening relation",
                GeometricCandidate, CitedStandard.PendingGeometric));

            s.Add(new CitedStandard("RM-97", "Intersection geometry",
                "Design-vehicle dimensions and minimum turning radii, the corner-radius selection relation, turn-lane "
                + "length and taper relations, and the sight-triangle leg relations",
                GeometricCandidate, CitedStandard.PendingGeometric));

            _standards = s;
            return new List<CitedStandard>(_standards);
        }

        /// <summary> The cited-standards reference: feature, quantity, source, edition. </summary>
        public static string StandardsTable()
        {
            string t = "| Task | Feature | Quantity taken from it | Source | Edition used |\n"
                + "| --- | --- | --- | --- | --- |";
            foreach (CitedStandard c in Standards())
            {
                t += "\n| " + c.task + " | " + c.feature + " | " + c.quantity + " | " + c.source
                    + " | " + c.edition + " |";
            }
            return t;
        }

        /// <summary> How many cite lines § 10 carries, and therefore how many rows the table must have. </summary>
        public const int CiteLineCount = 12;
    }
}
#endif
