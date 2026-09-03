#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-64's verifiable core: the consistency checklist for the fourteen author-facing editor files, as data
    /// rather than as prose.
    ///
    /// RM-64 asks for one consistency pass over every inspector in <c>Scripts/Editor/</c> and, critically, for "a
    /// checklist recording the state of each file" whose gate can tell a converted file from one that merely had its
    /// header renamed. A checklist kept as a hand-written table cannot do that: it records a claim, and nothing checks
    /// the claim. So the checklist lives here, the state of each file is DERIVED from which of the five criteria were
    /// applied rather than asserted, and RM-64's document is generated from this register.
    ///
    /// The five criteria are RM-64's own words, unchanged: fields grouped under the shared vocabulary, every numeric
    /// field carrying its unit, every refusal presented inline in the same style with its source named, the product name
    /// correct in every visible string, and foldout state persisted per component type. A file with all five is
    /// converted. A file with only the fourth is a renamed header, which RM-64 explicitly says is NOT converted -- and
    /// because the state is derived, no amount of optimism in a row can report it as one. </summary>

    /// <summary> The five things RM-64 requires per file, as flags so a row records exactly what was applied. </summary>
    [Flags]
    public enum ConversionCriteria
    {
        None = 0,
        /// <summary> Fields grouped under <see cref="FieldGroup"/>'s shared vocabulary, not ad-hoc headings. </summary>
        GroupedVocabulary = 1,
        /// <summary> Every numeric field drawn with its unit, from <see cref="FieldDescriptor.UnitLabel"/>. </summary>
        UnitOnEveryNumericField = 2,
        /// <summary> Constraints presented inline in one style, naming the limit AND its source (never a silent clamp). </summary>
        InlineConstraintWithSource = 4,
        /// <summary> Every visible product string is <see cref="InspectorFieldRegistry.ProductDisplayName"/> (C-05). </summary>
        ProductNameCorrect = 8,
        /// <summary> Foldout state persisted per component TYPE, so layout survives a selection change. </summary>
        FoldoutStatePersisted = 16,

        All = GroupedVocabulary | UnitOnEveryNumericField | InlineConstraintWithSource
            | ProductNameCorrect | FoldoutStatePersisted
    }

    /// <summary> What kind of editor file this is. The five criteria are not all meaningful for all of them -- a text
    /// pane has no numeric field to label -- and naming the kind is what makes a "deliberately unchanged" row reviewable
    /// instead of merely permitted. </summary>
    public enum InspectorKind
    {
        ComponentInspector,   // a CustomEditor over a component an author selects
        UtilityWindow,        // an EditorWindow: wizard, save, help, progress
        MenuOnly,             // menu items, no drawn layout of its own
        SharedHelper          // drawing helpers the others call; no inspector of its own
    }

    /// <summary> The four honest answers about a file. RM-64 names two of them -- converted, or deliberately unchanged
    /// with a reason -- and calls anything else an unexplained gap. Practice needs a third: a file that is known,
    /// scheduled, and not done yet. Recording that as "deliberately unchanged" would be a lie, and recording it as an
    /// unexplained gap would be a lie in the other direction, so <see cref="Pending"/> exists and carries the id of the
    /// task that owns the work. <see cref="NotConverted"/> is then exactly RM-64's unexplained gap, and the gate refuses
    /// it. </summary>
    public enum ConversionState
    {
        Converted,              // all five criteria applied
        DeliberatelyUnchanged,  // will not be converted, and the reason is recorded
        Pending,                // scheduled: an owning task id is recorded
        NotConverted            // neither -- the gap RM-64 forbids
    }

    /// <summary> One editor file's row. The state is a derived property, never a stored field, so a row cannot claim a
    /// conversion it did not do. </summary>
    public sealed class InspectorRecord
    {
        public readonly string file;      // file name within Scripts/Editor, verbatim
        public readonly string target;    // the component or window an author sees
        public readonly InspectorKind kind;
        public readonly ConversionCriteria applied;
        public readonly string reason;    // why it will not be converted; empty unless DeliberatelyUnchanged
        public readonly string owner;     // the RM id that owes the work; empty unless Pending
        public readonly string note;      // anything a reviewer needs that the columns do not carry

        public InspectorRecord(string file, string target, InspectorKind kind,
            ConversionCriteria applied, string reason, string owner, string note)
        {
            this.file = file; this.target = target; this.kind = kind; this.applied = applied;
            this.reason = reason ?? string.Empty; this.owner = owner ?? string.Empty;
            this.note = note ?? string.Empty;
        }

        /// <summary> Derived, in this order: all five criteria is converted; otherwise an owning task makes it pending;
        /// otherwise a recorded reason makes it deliberately unchanged; otherwise it is the gap. </summary>
        public ConversionState State
        {
            get
            {
                if (applied == ConversionCriteria.All) { return ConversionState.Converted; }
                if (owner.Length > 0) { return ConversionState.Pending; }
                if (reason.Length > 0) { return ConversionState.DeliberatelyUnchanged; }
                return ConversionState.NotConverted;
            }
        }

        /// <summary> The trap RM-64 names by hand: a file whose only change was the product string. It is never
        /// converted, and this predicate lets the gate say so about any row rather than about a remembered list. </summary>
        public bool IsHeaderRenameOnly
        {
            get { return applied == ConversionCriteria.ProductNameCorrect; }
        }

        /// <summary> Which of the five are still missing, for the document's last column. </summary>
        public ConversionCriteria Missing
        {
            get { return ConversionCriteria.All & ~applied; }
        }
    }

    /// <summary> Whether a message concerns one object or a whole build. </summary>
    public enum RefusalScope { PerObject, Aggregate }

    /// <summary> The two places a message can appear. </summary>
    public enum ReportPlacement { OnTheObjectInspector, InTheStatusPanel }

    /// <summary> RM-64's first settled convention, as a total function instead of a paragraph. Per-object refusals appear
    /// on the object; the aggregate appears in RM-52's status panel; "never both, never neither" is then a property of
    /// the mapping rather than a rule someone has to remember while writing the fourteenth inspector. Because the
    /// function is total and single-valued, a gate can assert both halves of that promise in two lines. </summary>
    public static class RefusalRouting
    {
        public static ReportPlacement Decide(RefusalScope scope)
        {
            return scope == RefusalScope.PerObject
                ? ReportPlacement.OnTheObjectInspector
                : ReportPlacement.InTheStatusPanel;
        }
    }

    /// <summary> RM-64's second settled convention: a value the tool COMPUTED must not look like a value the author may
    /// type. Measured lengths, solved radii, instance counts, RM-61's inscribed dimensions -- all of them are drawn
    /// disabled, in one group named here, and never in an editable field. Keeping the list in code means a new measured
    /// value is added in one place and inherits the presentation, rather than being drawn as an editable float by
    /// whoever adds it. </summary>
    public static class MeasuredValuePresentation
    {
        /// <summary> The one group heading measured values appear under. Not a <see cref="FieldGroup"/> member: the four
        /// groups describe what an author DECIDES, and a measurement is not a decision. </summary>
        public const string GroupLabel = "Measured";

        /// <summary> Never editable. Stated as a constant so a drawer cannot pass a different answer. </summary>
        public const bool IsEditable = false;

        private static readonly string[] _known =
        {
            "Measured centreline length (RM-75)",
            "Tightest circulatory radius and its station (RM-61)",
            "Inscribed circle diameter (RM-61)",
            "Entry path radius R1 (RM-61)",
            "Solved corner radius (RM-97)",
            "Superelevation runoff length (RM-96)",
            "Crest and sag curve length (RM-95)",
            "Placed instance count (RM-45)",
            "Suppressed terrain write count (RM-57)",
            "Stopping sight distance (RM-95)"
        };

        /// <summary> Every measured value the inspectors currently show, each naming the task that computes it. </summary>
        public static IList<string> Known()
        {
            return new List<string>(_known);
        }
    }

    /// <summary> The checklist itself: one row per author-facing editor file, and the list of files that are deliberately
    /// NOT author-facing so the disk scan has something to subtract. RM-64 said "all fifteen files", and fifteen is what
    /// <c>Scripts/Editor/</c> held once § 10's own tooling -- gates, the suite runner, the field model, the audits, the
    /// asset porting operators and the document writers -- was set aside. It is fourteen now: RM-101 deleted
    /// <c>EditorProgressWindow.cs</c>, the modal-bar demo, so the fifteenth file no longer exists to be converted. The
    /// tooling has grown since RM-64 and will keep growing; the inspectors have not. The gate compares this register
    /// against the directory, so adding a fifteenth inspector without a row fails the build rather than quietly escaping
    /// the pass. </summary>
    public static class InspectorConsistencyChecklist
    {
        private static List<InspectorRecord> _rows;

        /// <summary> Files in <c>Scripts/Editor/</c> that are § 10's own tooling rather than an author's inspector. Each
        /// is listed individually: a pattern like "*Gate.cs" would silently absorb a future inspector that happened to be
        /// called a gate.
        ///
        /// The test for membership is whether an author ever looks at the file's output, not whether the file is in the
        /// editor assembly. That covers the gates, the suite runner, the field model, the audits, the porting operators
        /// and the document writers, and it also covers two shapes that are neither: <c>BuildPumpDriver.cs</c>, which is
        /// a static frame source with no GUI of any kind, and <c>InspectorTextLayout.cs</c>, which draws nothing itself
        /// -- it sets the wrap flags and the label-column width that the fourteen files below then draw THROUGH. A
        /// conversion state for either would be a row about a file with no inspector to convert. </summary>
        private static readonly string[] _notAuthorFacing =
        {
            "ApproachMatchGate.cs",
            "AssemblyBoundaryAudit.cs",
            "AssetIdentityGate.cs",
            "AssetIdentityRegister.cs",
            "BuildProgressReporter.cs",
            "BuildPumpDriver.cs",
            "CompileGate.cs",
            "CompileLogReading.cs",
            "CrossSectionProfileGate.cs",
            "DispositionGate.cs",
            "DispositionRegister.cs",
            "DividedCarriagewayGate.cs",
            "Documentation.cs",
            "FlarePortOperator.cs",
            "HierarchyConnectionMarker.cs",
            "HierarchyMarkerGate.cs",
            "InspectorConsistencyChecklist.cs",
            "InspectorConsistencyDocWriter.cs",
            "InspectorConsistencyGate.cs",
            "InspectorFieldDrawer.cs",
            "InspectorFieldModel.cs",
            "InspectorFieldRegistry.cs",
            "InspectorFoldoutState.cs",
            "InspectorMetadataGate.cs",
            "InspectorStyleOwnershipGate.cs",
            "InspectorTextLayout.cs",
            "LaneSectionGate.cs",
            "LegacyInspectorGate.cs",
            "LegacyInspectorShell.cs",
            "LegacyMathGate.cs",
            "LegacyMathRegister.cs",
            "ManualReferences.cs",
            "MaterialPortGate.cs",
            "MaterialPortOperator.cs",
            "MaterialPortRecipes.cs",
            "MaterialPortWriter.cs",
            "PipelineAudit.cs",
            "PipelineVersionGate.cs",
            "RaoLibraryGate.cs",
            "RcmBrandGate.cs",
            "RcmBrandHeader.cs",
            "RcmDocumentWriters.cs",
            "RcmGateSuite.cs",
            "RcmPrepareShip.cs",
            "RcmScriptIcons.cs",
            "ShaderInventory.cs",
            "ShaderInventoryGate.cs",
            "ShaderInventoryWriter.cs",
            "ShipAuditGate.cs",
            "ShipAuditSweeps.cs",
            "StatusPanelGate.cs",
            "StreetLightFlareGate.cs",
            "StreetscapeGate.cs",
            "ToolManualGate.cs",
            "ToolManualWriter.cs",
            "VerticalAlignmentGate.cs",
            "CarriagewaySectionGate.cs",
            "IntersectionGeometryGate.cs"
        };

        /// <summary> The tooling exclusion list, for the gate and for the document's footnote. </summary>
        public static IList<string> NotAuthorFacing()
        {
            return new List<string>(_notAuthorFacing);
        }

        private static InspectorRecord Row(string file, string target, InspectorKind kind,
            ConversionCriteria applied, string reason, string owner, string note)
        {
            return new InspectorRecord(file, target, kind, applied, reason, owner, note);
        }

        /// <summary> The fourteen rows, built once, in file-name order so the generated document is stable. </summary>
        public static IList<InspectorRecord> Rows()
        {
            if (_rows != null) { return new List<InspectorRecord>(_rows); }
            List<InspectorRecord> r = new List<InspectorRecord>();

            r.Add(Row("EditorMenu.cs", "Menu items", InspectorKind.MenuOnly,
                ConversionCriteria.ProductNameCorrect,
                "Menu paths only. It draws no fields, so four of the five criteria have nothing to attach to; the fifth, the product string, is the one that applies and is applied.",
                "", "Menu path root is the display name."));

            // RM-101 removed the EditorProgressWindow.cs row along with the file. That window's only content was a
            // seconds field and a button that raised a MODAL progress bar for that many seconds -- a vendor demo, with a
            // private Init() no menu item ever called. It was never an author's inspector, so there is nothing here to
            // convert and nothing to move to _notAuthorFacing: the file is gone, and the count is fourteen.

            r.Add(Row("EditorUtilities.cs", "Shared drawing helpers", InspectorKind.SharedHelper,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "The helpers the legacy inspectors draw through. RM-102 routes them at InspectorFieldDrawer, which is where units and inline constraints actually arrive."));

            r.Add(Row("HelpWindow.cs", "Help", InspectorKind.UtilityWindow,
                ConversionCriteria.ProductNameCorrect,
                "A text pane with no author-editable numeric field and no constraint to present. Imposing a grouping vocabulary on three paragraphs is churn, which RM-64 names as the thing a consistency pass must not become.",
                "", "Reviewed 2026-08-30."));

            r.Add(Row("ObjExporter.cs", "Mesh export command", InspectorKind.UtilityWindow,
                ConversionCriteria.ProductNameCorrect,
                "An export command behind a file dialog, with no editable field set. Its one real defect is RM-63.7's swallowed texture-copy failure, which is a correctness fix rather than a layout one.",
                "", "See RM-63.7."));

            r.Add(Row("RoadEditor.cs", "Road", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "The inspector an author lives in. Its fields are already described by RM-51's registry; what is missing is the drawer that reads those descriptors instead of the hand-rolled layout."));

            r.Add(Row("RoadIntersectionEditor.cs", "RoadIntersection", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "Also gains RM-97's solved corner radius and sight triangle as MEASURED values, so it must adopt MeasuredValuePresentation rather than draw them as editable floats."));

            r.Add(Row("RoadSystemEditor.cs", "RoadSystem", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "Carries RM-52's status panel, so it is where the AGGREGATE half of RefusalRouting lands. Per-object refusals must NOT be repeated here."));

            r.Add(Row("SaveWindow.cs", "Group save", InspectorKind.UtilityWindow,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "Layout by RM-102, filename determinism by RM-63.6, and the .rao schema it writes by RM-105 -- three owners, one window."));

            r.Add(Row("SplineCEditor.cs", "SplineC", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "The alignment component RM-62 makes the head of the chain; RM-95 adds the vertical profile fields it must group."));

            r.Add(Row("SplineFEditor.cs", "SplineF", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102", string.Empty));

            r.Add(Row("SplineIEditor.cs", "SplineI", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102", string.Empty));

            r.Add(Row("SplineNEditor.cs", "SplineN", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "The largest of the family and the one RM-75 names for its node-editing stall; its conversion and that stall are the same read of the same file."));

            r.Add(Row("TerrainEditor.cs", "Terrain interaction", InspectorKind.ComponentInspector,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "Its fields decide what RM-57's gateway is allowed to write, so its inline constraints are the ones an author most needs to see before a build, not after."));

            r.Add(Row("Wizard.cs", "Setup wizard", InspectorKind.UtilityWindow,
                ConversionCriteria.ProductNameCorrect, "", "RM-102",
                "A stepped dialog: the grouping vocabulary maps onto its steps rather than onto foldouts, which is a documented deviation and not an exemption."));

            _rows = r;
            return new List<InspectorRecord>(_rows);
        }

        /// <summary> How many rows the checklist carries. Fourteen since RM-101 deleted the modal-bar demo window; the
        /// gate asserts it against the directory rather than against this number. </summary>
        public static int Count { get { return Rows().Count; } }

        /// <summary> Rows in one state, for the gate and the document's summary line. </summary>
        public static IList<InspectorRecord> InState(ConversionState state)
        {
            List<InspectorRecord> hits = new List<InspectorRecord>();
            foreach (InspectorRecord row in Rows())
            {
                if (row.State == state) { hits.Add(row); }
            }
            return hits;
        }

        /// <summary> The five criteria spelled out, so the document reads as English rather than as a bit pattern. </summary>
        public static string CriteriaText(ConversionCriteria c)
        {
            if (c == ConversionCriteria.None) { return "none"; }
            List<string> parts = new List<string>();
            if ((c & ConversionCriteria.GroupedVocabulary) != 0) { parts.Add("grouping"); }
            if ((c & ConversionCriteria.UnitOnEveryNumericField) != 0) { parts.Add("units"); }
            if ((c & ConversionCriteria.InlineConstraintWithSource) != 0) { parts.Add("inline constraints"); }
            if ((c & ConversionCriteria.ProductNameCorrect) != 0) { parts.Add("product name"); }
            if ((c & ConversionCriteria.FoldoutStatePersisted) != 0) { parts.Add("foldout persistence"); }
            return string.Join(", ", parts.ToArray());
        }

        private static string StateText(ConversionState s)
        {
            switch (s)
            {
                case ConversionState.Converted: return "converted";
                case ConversionState.DeliberatelyUnchanged: return "deliberately unchanged";
                case ConversionState.Pending: return "pending";
                default: return "NOT CONVERTED";
            }
        }

        /// <summary> The generated markdown table RM-64's document carries between its markers. One row per file, the
        /// state derived, and the reason or owner in the last column so a reviewer never has to ask why a file is not
        /// converted. </summary>
        public static string ToManualTable()
        {
            StringBuilder b = new StringBuilder();
            b.Append("| File | Author sees | Kind | State | Applied | Still missing | Why |\n");
            b.Append("| --- | --- | --- | --- | --- | --- | --- |\n");
            foreach (InspectorRecord row in Rows())
            {
                string why = row.owner.Length > 0
                    ? "Owned by " + row.owner + ". " + row.note
                    : (row.reason.Length > 0 ? row.reason + " " + row.note : row.note);
                b.Append("| `").Append(row.file).Append("` | ").Append(row.target)
                 .Append(" | ").Append(row.kind.ToString())
                 .Append(" | ").Append(StateText(row.State))
                 .Append(" | ").Append(CriteriaText(row.applied))
                 .Append(" | ").Append(CriteriaText(row.Missing))
                 .Append(" | ").Append(why.Trim()).Append(" |\n");
            }
            return b.ToString().TrimEnd('\n');
        }
    }
}
#endif
