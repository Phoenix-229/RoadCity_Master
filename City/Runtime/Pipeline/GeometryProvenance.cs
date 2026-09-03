#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which component actually produces a feature's geometry. The first four are the vendored road
    /// construction chain and are the ONLY mesh builders in the tool; the next two are the city layer's operator
    /// pipeline for area and heightfield work; `NoGeometry` is an honest answer for a rule, a validator, a data model
    /// or a piece of UI; and `LoggedGap` is the narrow escape hatch, which is not a licence to write a second mesher
    /// but an obligation to log the feature in § 10.9 with the reason BEFORE any alternative code exists. </summary>
    public enum GeometryPath
    {
        SplineC = 0,                     // alignment: position, tangent, arc-length mapping
        RoadConstructorBufferMaker = 1,  // the road surface itself: cut and vertex data
        SplinatedMeshMaker = 2,          // sweep an arbitrary cross-section along a curve
        EdgeObjectMaker = 3,             // place prefabs at a spacing along a curve
        CityAreaOperator = 4,            // closed area / polygon work through § 10.4's operators
        HeightfieldOperator = 5,         // terrain heightfield work through § 10.4's operators
        NoGeometry = 6,                  // produces no geometry at all
        LoggedGap = 7                    // genuinely none of the above: § 10.9 must carry it
    }

    /// <summary> The SHAPE of a feature, which is what the decision procedure actually runs on. RM-62's rule is not
    /// "use the chain because it is nicer": it is a procedure -- sweep or spacing goes to the chain, area or
    /// heightfield goes to the city operators, data goes nowhere, and only something that is genuinely none of those
    /// may be logged as a gap. Declaring the shape separately from the path is what makes the register CHECKABLE: a
    /// row whose declared path does not follow from its declared shape is a row someone talked themselves into. </summary>
    public enum FeatureShape
    {
        SweptProfile = 0,      // a cross-section swept along an alignment
        RoadSurface = 1,       // the carriageway's own width, cut and vertices
        Alignment = 2,         // the curve itself: position, tangent, station
        SpacedPrefabs = 3,     // objects repeated along a curve
        ClosedArea = 4,        // a polygon or closed surface, not a sweep
        Heightfield = 5,       // terrain samples
        DataOnly = 6,          // rule, contract, validator, model, inspector, status
        Neither = 7            // must be logged in § 10.9
    }

    /// <summary> One row of RM-62's provenance register: the task, what it delivers, the shape of that delivery, the
    /// component that builds it, an optional second component (a bridge deck is a sweep whose piers are spaced
    /// prefabs -- pretending features have exactly one producer would force a false answer), and the BASIS: the
    /// sentence a reviewer reads instead of taking the row on trust. </summary>
    [Serializable]
    public struct ProvenanceRow
    {
        public int taskNumber;
        public string feature;
        public FeatureShape shape;
        public GeometryPath primary;
        public GeometryPath secondary;   // NoGeometry when the feature has a single producer
        public string basis;

        public string TaskId
        {
            get { return "RM-" + taskNumber.ToString("00", CultureInfo.InvariantCulture); }
        }
    }

    /// <summary> RM-62's rule, written as code rather than as an aspiration: the register below names, for every one
    /// of RM-26 ... RM-61, which component produces its geometry, and `Decide` is the procedure that answer has to
    /// follow. The rule exists because a second mesh-building path costs twice for every fix afterwards -- every
    /// arc-length correction, LOD change, UV convention and determinism guarantee would have to be made in two places,
    /// and the second copy falls behind silently. The classic symptom is a sidewalk whose UVs stretch through curves
    /// because a bespoke mesher never received the fix the road surface got.
    ///
    /// What this file can prove without a scene: that every task in the range has a row, that no row invents a
    /// producer, that each row's declared component FOLLOWS from the shape of what it delivers, that nothing sits in
    /// the escape hatch without a § 10.9 entry, and that the four chain components plus the two city operators are the
    /// complete set of producers. What it cannot prove is that the code actually calls them -- that is a review
    /// judgement and a source scan, and the gate does the scan. </summary>
    public static class GeometryProvenanceRegister
    {
        /// <summary> The lowest and highest task the register covers. RM-62 itself is the rule, so it is not a row. </summary>
        public const int FirstTask = 26;
        public const int LastTask = 61;

        /// <summary> The decision procedure, in the order the task states it: sweep or spacing -> the vendored chain;
        /// area or heightfield -> the city operators; data -> nothing; genuinely neither -> log it in § 10.9. A feature
        /// arriving here as `Neither` does NOT get permission to build its own mesher; it gets an obligation. </summary>
        public static GeometryPath Decide(FeatureShape shape)
        {
            switch (shape)
            {
                case FeatureShape.Alignment: return GeometryPath.SplineC;
                case FeatureShape.RoadSurface: return GeometryPath.RoadConstructorBufferMaker;
                case FeatureShape.SweptProfile: return GeometryPath.SplinatedMeshMaker;
                case FeatureShape.SpacedPrefabs: return GeometryPath.EdgeObjectMaker;
                case FeatureShape.ClosedArea: return GeometryPath.CityAreaOperator;
                case FeatureShape.Heightfield: return GeometryPath.HeightfieldOperator;
                case FeatureShape.DataOnly: return GeometryPath.NoGeometry;
                default: return GeometryPath.LoggedGap;
            }
        }

        /// <summary> Whether a path is one of the four vendored chain components -- the tool's only mesh builders. </summary>
        public static bool IsVendoredChain(GeometryPath path)
        {
            return path == GeometryPath.SplineC || path == GeometryPath.RoadConstructorBufferMaker
                || path == GeometryPath.SplinatedMeshMaker || path == GeometryPath.EdgeObjectMaker;
        }

        /// <summary> Whether a path is one of § 10.4's city operators: area work and heightfield work, which are
        /// legitimately not sweeps and are the reason the escape hatch stays narrow. </summary>
        public static bool IsCityOperator(GeometryPath path)
        {
            return path == GeometryPath.CityAreaOperator || path == GeometryPath.HeightfieldOperator;
        }

        /// <summary> Whether a row obliges someone to write a § 10.9 gap entry before any code. </summary>
        public static bool RequiresGapLog(GeometryPath path)
        {
            return path == GeometryPath.LoggedGap;
        }

        #region "The register"

        private static ProvenanceRow[] _rows;

        /// <summary> Build one row. Present so the table below reads as a table and not as forty blocks of assignments;
        /// `secondary` is `NoGeometry` for the common single-producer case. </summary>
        private static ProvenanceRow Row(int task, string feature, FeatureShape shape,
            GeometryPath primary, GeometryPath secondary, string basis)
        {
            ProvenanceRow r = new ProvenanceRow();
            r.taskNumber = task;
            r.feature = feature;
            r.shape = shape;
            r.primary = primary;
            r.secondary = secondary;
            r.basis = basis;
            return r;
        }

        /// <summary> The register, copied on the way out. A caller holding a reference to the live array could edit the
        /// tool's own record of where its geometry comes from, which is exactly the kind of quiet change this task
        /// exists to prevent. </summary>
        public static ProvenanceRow[] Rows()
        {
            if (_rows == null) { _rows = Build(); }
            ProvenanceRow[] copy = new ProvenanceRow[_rows.Length];
            Array.Copy(_rows, copy, _rows.Length);
            return copy;
        }

        /// <summary> How many rows the register carries. RM-26 ... RM-61 is 36 tasks, so this must be 36. </summary>
        public static int Count
        {
            get { if (_rows == null) { _rows = Build(); } return _rows.Length; }
        }
        /// <summary> One row per task in RM-26 ... RM-61, in order. The `basis` is the part that matters: it is the
        /// sentence that makes the row reviewable, and it says WHY the shape is what it is, not merely that it is. </summary>
        private static ProvenanceRow[] Build()
        {
            List<ProvenanceRow> r = new List<ProvenanceRow>(40);

            r.Add(Row(26, "Road terminus treatment", FeatureShape.SpacedPrefabs,
                GeometryPath.EdgeObjectMaker, GeometryPath.NoGeometry,
                "The treatment is a set of discrete objects -- barrier, terminal, marker, buffer -- placed at stations "
                + "computed from the road's end. Placement at a spacing along a curve is exactly what EdgeObjectMaker "
                + "does; the carriageway being terminated is still built by the road surface path."));

            r.Add(Row(27, "Lane-count mismatch: the merge taper", FeatureShape.RoadSurface,
                GeometryPath.RoadConstructorBufferMaker, GeometryPath.NoGeometry,
                "A taper is the carriageway's own width changing over a length, so it is width input to the surface "
                + "builder and not a separate object. Building it anywhere else would put two different sets of edge "
                + "vertices on the same road and leave a seam wherever they disagreed."));

            r.Add(Row(28, "Roundabout as a junction type", FeatureShape.ClosedArea,
                GeometryPath.CityAreaOperator, GeometryPath.RoadConstructorBufferMaker,
                "The circulatory way is a closed loop and SplineC does not support one, so the loop cannot be a road: "
                + "the annulus and the central island are area work through § 10.4's operators. The approach legs and "
                + "their flares are ordinary carriageway and stay with the surface builder."));

            r.Add(Row(29, "Sidewalk, including corners", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.NoGeometry,
                "A footway with a kerb is a fixed cross-section swept along an offset alignment. Turning a corner is an "
                + "alignment problem -- a corner radius on the curve -- not a reason to build a polygon by hand."));

            r.Add(Row(30, "Culverts and roadside ditches", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.EdgeObjectMaker,
                "The ditch is a trapezoidal section swept along the road, so it sweeps. The barrels, headwalls and "
                + "aprons are discrete objects at crossing stations, so they are placed. Two producers here is the "
                + "honest answer, not a hedge."));

            r.Add(Row(31, "Variable-width cross-section elements", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.NoGeometry,
                "The sweeper already samples the profile once per station, so a width that varies with station is data "
                + "for the existing sweep rather than a new kind of geometry."));
            r.Add(Row(32, "Pavement markings", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.EdgeObjectMaker,
                "Longitudinal lines are thin ribbons swept along the alignment and must follow the same arc-length "
                + "mapping as the surface under them, or the dashes drift out of step through curves. Symbols -- "
                + "arrows, stop lines, legends -- are single objects at a station, so they are placed."));

            r.Add(Row(33, "Bridge types beyond the flyover", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.EdgeObjectMaker,
                "A deck is a cross-section swept along the crossing alignment; piers, towers and bearings repeat at a "
                + "spacing underneath it. This is the row that proves a feature can honestly have two producers: "
                + "forcing one answer would either sweep the piers or place the deck, and both are wrong."));

            r.Add(Row(34, "Retaining structure on elevated road edges", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.NoGeometry,
                "A wall is a section -- face, footing, coping -- swept along the road edge, with its height driven per "
                + "station by the drop to the ground. Sweeping it keeps it welded to the edge it retains."));

            r.Add(Row(35, "Automatic grade separation when two roads cross", FeatureShape.Alignment,
                GeometryPath.SplineC, GeometryPath.NoGeometry,
                "Separation is achieved by moving node elevations so the two alignments clear each other. The "
                + "deliverable is the changed curve, which is SplineC's; the carriageways then rebuild themselves from "
                + "it, so nothing here produces a surface of its own."));

            r.Add(Row(36, "Aerial ropeway: cable car and gondola lines", FeatureShape.SpacedPrefabs,
                GeometryPath.EdgeObjectMaker, GeometryPath.SplinatedMeshMaker,
                "Towers and cabins repeat along the line, so they are placed. The cable itself is a tube swept along a "
                + "catenary alignment -- a sweep with an unusual vertical curve, which is still a sweep."));

            r.Add(Row(37, "Tunnel cavity requests, portals, and the underwater seal", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.HeightfieldOperator,
                "The lining is a bore section swept along the alignment. The portal's notch into the hillside is ground, "
                + "not lining, so it is heightfield work through § 10.4's operators."));
            r.Add(Row(38, "Fix the terrain footprint: the road moves too much dirt", FeatureShape.Heightfield,
                GeometryPath.HeightfieldOperator, GeometryPath.NoGeometry,
                "The whole subject of the task is which terrain samples change and by how much, so it is heightfield "
                + "work by definition. No mesh is produced: the ground is Unity's Terrain."));

            r.Add(Row(39, "Cut slopes that daylight into the ground", FeatureShape.Heightfield,
                GeometryPath.HeightfieldOperator, GeometryPath.NoGeometry,
                "Daylighting means walking outward from the road edge until the slope meets the existing ground, and "
                + "writing the samples in between. That is a heightfield operation, not a wall to be swept."));

            r.Add(Row(40, "Superelevation: banking through curves", FeatureShape.RoadSurface,
                GeometryPath.RoadConstructorBufferMaker, GeometryPath.NoGeometry,
                "Banking rotates the carriageway's cross-section about its crown, which changes the surface's own "
                + "vertex heights. Anything swept alongside -- kerbs, markings, barriers -- inherits the rotation from "
                + "the surface rather than computing it a second time."));

            r.Add(Row(41, "Unsealed roads as a first-class surface type", FeatureShape.RoadSurface,
                GeometryPath.RoadConstructorBufferMaker, GeometryPath.NoGeometry,
                "An unsealed road is the same carriageway with a different width, crown, edge treatment and material. "
                + "It is a parameter set for the surface builder; a second builder for gravel would be the exact "
                + "duplication this rule forbids."));

            r.Add(Row(42, "One zone taxonomy, and what a zone carries", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "A taxonomy is a shared vocabulary read by the things that do build. It has no vertices, and saying so "
                + "plainly is more useful than pretending every task is a mesher."));

            r.Add(Row(43, "Transport hubs as authored anchors", FeatureShape.ClosedArea,
                GeometryPath.CityAreaOperator, GeometryPath.NoGeometry,
                "A hub's apron or platform slab is a closed footprint, not a sweep along a curve, so it goes to the area "
                + "operators. Its hard connection requirements are satisfied by ordinary roads built the ordinary way."));
            r.Add(Row(44, "Paint the settlement footprint as a mask", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "The deliverable is a mask the author paints. It decides where later tasks build, and builds nothing "
                + "itself."));

            r.Add(Row(45, "The density and zone masks: what the paint means", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "Reading a channel and stating what its value means is interpretation, not geometry."));

            r.Add(Row(46, "Conform the painted footprint to water and slope", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "It reads the heightfield and the water surfaces and then edits the MASK. Reading terrain is not "
                + "heightfield work; writing terrain would be, and this task writes none."));

            r.Add(Row(47, "Street furniture and vegetation resolved at bake time", FeatureShape.SpacedPrefabs,
                GeometryPath.EdgeObjectMaker, GeometryPath.CityAreaOperator,
                "Furniture along a street is spacing along a curve. Scatter inside a block is a density over an area, "
                + "which is why the area operators appear as the second producer instead of a bespoke scatterer."));

            r.Add(Row(48, "Import contract and validator for user prefabs", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "A contract states what an incoming prefab must satisfy and a validator checks it. Both inspect "
                + "geometry someone else authored; neither creates any."));

            r.Add(Row(49, "Full-tree scan of the vendored road tool", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "The output is written findings. This register is one of them."));

            r.Add(Row(50, "Field census and the non-breaking contract", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "Naming every serialised field and promising not to break it is a data contract."));
            r.Add(Row(51, "Rebuild the inspector: grouping, units, validation", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "An inspector edits values. The values then reach the builders above."));

            r.Add(Row(52, "The status panel: mode, provider, last build, refusals", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "A panel that reports state. Its whole value is that it does not change anything it reports."));

            r.Add(Row(53, "Connection indicator on the hierarchy row", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "A drawn marker in the editor's own hierarchy window is not scene geometry."));

            r.Add(Row(54, "A stated City sub-hierarchy for generated objects", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "It creates empty parent transforms in an agreed order. A transform carries no vertices; what gets "
                + "parented into it was built by one of the producers above."));

            r.Add(Row(55, "Fail closed: no grey box, no silent substitution", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "The resolver's entire purpose is to REFUSE rather than invent geometry, so a row claiming it produced "
                + "any would contradict the task. Its own gate scans this tree for primitive-cube creation for exactly "
                + "the same reason, and that scan is why this sentence does not spell the call out."));

            r.Add(Row(56, "Show where every category's content comes from", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "It lists and validates the author's own assets."));

            r.Add(Row(57, "Route terrain modification through the provider", FeatureShape.Heightfield,
                GeometryPath.HeightfieldOperator, GeometryPath.NoGeometry,
                "This is the one file allowed to write TerrainData, so it is the heightfield producer by construction. "
                + "When a provider is connected it writes nothing and hands the regions over instead, which is a "
                + "different destination for the same operation, not a different kind of geometry."));
            r.Add(Row(58, "Keep the standalone path working, and say when a provider is needed", FeatureShape.DataOnly,
                GeometryPath.NoGeometry, GeometryPath.NoGeometry,
                "A per-feature availability table plus the sentence that tells the author what to connect."));

            r.Add(Row(59, "A divided road is two carriageways", FeatureShape.SweptProfile,
                GeometryPath.SplinatedMeshMaker, GeometryPath.RoadConstructorBufferMaker,
                "What RM-59 delivers is ONE cross-section emitter that produces the same vertices for a divided and an "
                + "undivided road, which makes it profile data for the sweep. The two carriageway surfaces themselves "
                + "are still the surface builder's, which is why it is named second."));

            r.Add(Row(60, "What is in the median: trough, barrier ends, planting", FeatureShape.SpacedPrefabs,
                GeometryPath.EdgeObjectMaker, GeometryPath.SplinatedMeshMaker,
                "Barriers, their terminals and planting are objects placed along the median at a spacing. The drainage "
                + "trough is a channel section swept along it. The width band decides which of the two applies."));

            r.Add(Row(61, "Shape-flexible roundabout: ellipse or control polygon", FeatureShape.ClosedArea,
                GeometryPath.CityAreaOperator, GeometryPath.NoGeometry,
                "The shape is a closed curve by definition and SplineC cannot close, so the circulatory way and island "
                + "are area work. Everything the task actually computes -- per-station curvature, entry skew, splitter "
                + "length -- is measurement on that closed curve, and measurement produces no vertices at all."));

            return r.ToArray();
        }
        #endregion

        #region "Queries a reviewer or a gate can run"

        /// <summary> The row for one task, or false when the register does not carry it. </summary>
        public static bool For(int taskNumber, out ProvenanceRow row)
        {
            if (_rows == null) { _rows = Build(); }
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].taskNumber == taskNumber) { row = _rows[i]; return true; }
            }
            row = new ProvenanceRow();
            return false;
        }

        /// <summary> Tasks in RM-26 ... RM-61 with no row. An empty list is the only acceptable answer: a task that is
        /// missing here is a task whose geometry nobody has had to account for. </summary>
        public static List<int> MissingTasks()
        {
            if (_rows == null) { _rows = Build(); }
            List<int> missing = new List<int>();
            for (int t = FirstTask; t <= LastTask; t++)
            {
                bool found = false;
                for (int i = 0; i < _rows.Length && !found; i++)
                {
                    if (_rows[i].taskNumber == t) { found = true; }
                }
                if (!found) { missing.Add(t); }
            }
            return missing;
        }

        /// <summary> Task numbers carrying more than one row, and task numbers outside the covered range. Two rows for
        /// one task means two different answers to the same question, which is worse than none. </summary>
        public static List<int> MalformedTasks()
        {
            if (_rows == null) { _rows = Build(); }
            List<int> bad = new List<int>();
            for (int i = 0; i < _rows.Length; i++)
            {
                int t = _rows[i].taskNumber;
                if (t < FirstTask || t > LastTask) { if (!bad.Contains(t)) { bad.Add(t); } continue; }
                for (int j = i + 1; j < _rows.Length; j++)
                {
                    if (_rows[j].taskNumber == t && !bad.Contains(t)) { bad.Add(t); }
                }
            }
            return bad;
        }
        /// <summary> Rows whose declared primary producer does NOT follow from the shape they declare. This is the check
        /// with teeth: it catches the row someone argued into a convenient answer, because the shape and the component
        /// were written down separately and the procedure connects them. </summary>
        public static List<ProvenanceRow> RowsThatDoNotFollow()
        {
            if (_rows == null) { _rows = Build(); }
            List<ProvenanceRow> bad = new List<ProvenanceRow>();
            for (int i = 0; i < _rows.Length; i++)
            {
                if (Decide(_rows[i].shape) != _rows[i].primary) { bad.Add(_rows[i]); }
            }
            return bad;
        }

        /// <summary> Rows with nothing written in the basis column. A row without a basis is an assertion. </summary>
        public static List<ProvenanceRow> RowsWithoutBasis()
        {
            if (_rows == null) { _rows = Build(); }
            List<ProvenanceRow> bad = new List<ProvenanceRow>();
            for (int i = 0; i < _rows.Length; i++)
            {
                string b = _rows[i].basis;
                if (string.IsNullOrEmpty(b) || b.Trim().Length < 20) { bad.Add(_rows[i]); }
            }
            return bad;
        }

        /// <summary> Rows sitting in the escape hatch, each of which owes § 10.9 an entry BEFORE any alternative code
        /// exists. The register is expected to return none: if it ever returns one, the correct response is to write the
        /// gap entry, not to widen the enum. </summary>
        public static List<ProvenanceRow> GapsRequiringLog()
        {
            if (_rows == null) { _rows = Build(); }
            List<ProvenanceRow> gaps = new List<ProvenanceRow>();
            for (int i = 0; i < _rows.Length; i++)
            {
                if (RequiresGapLog(_rows[i].primary) || RequiresGapLog(_rows[i].secondary)) { gaps.Add(_rows[i]); }
            }
            return gaps;
        }

        /// <summary> How many rows name a given producer, counting a secondary as a use. Lets a reviewer see at a glance
        /// that all four chain components are actually carrying work and that no producer is a dead letter. </summary>
        public static int UseCount(GeometryPath path)
        {
            if (_rows == null) { _rows = Build(); }
            int n = 0;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].primary == path) { n++; }
                else if (_rows[i].secondary == path && path != GeometryPath.NoGeometry) { n++; }
            }
            return n;
        }
        #endregion

        #region "Rendering the register for the manual"

        /// <summary> The producer's name as the manual prints it. These strings are part of the drift check, so they are
        /// fixed here rather than taken from `ToString()`, which would change under a rename of the enum. </summary>
        public static string PathName(GeometryPath path)
        {
            switch (path)
            {
                case GeometryPath.SplineC: return "SplineC (alignment)";
                case GeometryPath.RoadConstructorBufferMaker: return "RoadConstructorBufferMaker (road surface)";
                case GeometryPath.SplinatedMeshMaker: return "SplinatedMeshMaker (swept section)";
                case GeometryPath.EdgeObjectMaker: return "EdgeObjectMaker (placed objects)";
                case GeometryPath.CityAreaOperator: return "City area operator (10.4)";
                case GeometryPath.HeightfieldOperator: return "City heightfield operator (10.4)";
                case GeometryPath.NoGeometry: return "none";
                default: return "LOGGED GAP (10.9)";
            }
        }

        /// <summary> The shape's name as the manual prints it, for the same reason. </summary>
        public static string ShapeName(FeatureShape shape)
        {
            switch (shape)
            {
                case FeatureShape.SweptProfile: return "swept profile";
                case FeatureShape.RoadSurface: return "road surface";
                case FeatureShape.Alignment: return "alignment";
                case FeatureShape.SpacedPrefabs: return "spaced objects";
                case FeatureShape.ClosedArea: return "closed area";
                case FeatureShape.Heightfield: return "heightfield";
                case FeatureShape.DataOnly: return "data only";
                default: return "NEITHER";
            }
        }

        /// <summary> A cell's text with any pipe escaped, so one stray character cannot silently break the table. </summary>
        private static string Bar(string cell)
        {
            if (string.IsNullOrEmpty(cell)) { return string.Empty; }
            return cell.Replace("|", "\\|");
        }

        /// <summary> The register as the markdown table the manual carries between its two markers. Newlines are always
        /// a single "\n" so a drift comparison cannot fail merely because an editor rewrote the line endings. </summary>
        public static string ToManualTable()
        {
            if (_rows == null) { _rows = Build(); }
            StringBuilder sb = new StringBuilder(16384);
            sb.Append("| Task | Feature | Shape | Produced by | Also | Basis |\n");
            sb.Append("| --- | --- | --- | --- | --- | --- |\n");
            for (int i = 0; i < _rows.Length; i++)
            {
                ProvenanceRow r = _rows[i];
                sb.Append("| ").Append(r.TaskId);
                sb.Append(" | ").Append(Bar(r.feature));
                sb.Append(" | ").Append(ShapeName(r.shape));
                sb.Append(" | ").Append(PathName(r.primary));
                sb.Append(" | ").Append(r.secondary == GeometryPath.NoGeometry ? "-" : PathName(r.secondary));
                sb.Append(" | ").Append(Bar(r.basis)).Append(" |\n");
            }
            return sb.ToString();
        }

        #endregion
    }
}
