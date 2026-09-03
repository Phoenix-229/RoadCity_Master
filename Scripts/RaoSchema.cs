#region "Imports"
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-105's documented schema for the <c>.rao</c> library format: every element the tool writes, grouped
    /// into families, each family saying what its numbers MEAN and in what units.
    ///
    /// ---- WHY THIS IS CODE AND NOT A DOCUMENT ----
    /// A schema written as prose in a file next to the code is correct exactly once. Fields get added to
    /// <c>SplinatedMeshLibraryMaker</c> as RM-31's generalised cross-section lands, <c>XmlSerializer</c> writes them
    /// out without being told to, and the document goes quietly stale — which is worse than no document, because the
    /// next person reads it and believes it. Here the member names are read back off the types by reflection, and
    /// <c>RaoLibraryGate</c> requires the families to partition that reflected set EXACTLY: no member unclassified, no
    /// member claimed twice, no name listed that no longer exists. Add a field and the build's own gate fails until
    /// somebody says what the field is and what it is measured in. That is the only kind of schema that stays true.
    ///
    /// ---- WHAT THE FAMILIES ARE FOR ----
    /// Grouping is not decoration. The RM-86 audit turns on a distinction that is invisible from a flat field list: a
    /// number in one of these files is a PLACEMENT (where in the cross-section the author put a railing — authored, no
    /// external standard governs it), a REFERENCE (an asset path), a SOLVER TOLERANCE (a meshing epsilon), or a
    /// COLLISION PROXY (an extent computed from the referenced <c>.fbx</c>, not an independently authored dimension).
    /// Only the first of those could ever be checked against a published figure, and the families are what let a reader
    /// tell which is which before reaching for a standard that does not apply. See
    /// <see cref="DataAssetAuditRegister"/>, which audits the same numbers value by value.
    ///
    /// Units throughout: distances are METRES, because the tool's world is metres and a <c>.rao</c> is read straight
    /// into transforms. Rotations are DEGREES (Euler, Unity's convention). <c>startTime</c>/<c>endTime</c> and their
    /// capitalised twins are FRACTIONS of the spline segment, 0 at its start and 1 at its end — named "time" by the
    /// original author after the spline parameter, not after anything measured in seconds. </summary>
    public static class RaoSchema
    {
        /// <summary> One named group of elements, with the sentence that says what they are. <c>members</c> is the
        /// exact set of serialised element names this family claims — exact names rather than a prefix rule, because a
        /// prefix silently absorbs the next field somebody adds and the whole point here is that it must not. </summary>
        public sealed class Family
        {
            public readonly string name;
            public readonly string note;
            public readonly string[] members;


            public Family(string _name, string _note, string[] _members)
            {
                name = _name;
                note = _note;
                members = _members;
            }
        }


        // ------------------------------------------------------------------ reflection ------

        /// <summary> The element names <c>XmlSerializer</c> will actually write for a record type, in declaration
        /// order. Public instance FIELDS only: the serialiser writes public fields and public properties and ignores
        /// methods, and these three record classes carry no public properties — which is exactly why
        /// <c>IRaoRecord.RaoVersion()</c> is a method (a property would appear here, and in every written file). </summary>
        public static string[] ElementNames(System.Type _recordType)
        {
            FieldInfo[] fields = _recordType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            int count = fields.Length;
            string[] names = new string[count];
            for (int index = 0; index < count; index++)
            {
                names[index] = fields[index].Name;
            }
            return names;
        }


        // ------------------------------------------------------------------ the families ------

        private const string NoteStamp =
            "The format version the file declares. Absent or 0 means a legacy file written before RM-105 and is read "
            + "as version 1; the reader refuses anything above RaoFormat.CurrentVersion by number rather than guessing "
            + "at fields it has never seen. Initialised to 0 deliberately: XmlSerializer runs field initialisers BEFORE "
            + "it applies elements, so any other default would forge a stamp onto files that carry none.";

        private static readonly Family[] _wizard =
        {
            new Family("Format stamp", NoteStamp, new[] { "formatVersion" }),

            new Family("Identity and presentation",
                "What the group preset calls itself in the wizard grid, and the asset path of the thumbnail drawn on "
                + "its button. fileName is the preset's own file name carried in the record so a renamed file still "
                + "reports the name it was saved as. isDefault marks a preset shipped with the tool rather than one "
                + "the author saved, and isBridge is what the wizard's Bridges tab filters on. No dimensions here.",
                new[] { "displayName", "desc", "thumbString", "fileName", "isDefault", "isBridge" })
        };

        private static readonly Family[] _edgeObject =
        {
            new Family("Format stamp", NoteStamp, new[] { "formatVersion" }),

            new Family("Identity and presentation",
                "Naming and wizard-grid presentation only. isToggled is the inspector's fold-out state, saved so a "
                + "reopened road looks the way the author left it.",
                new[] { "objectName", "displayName", "desc", "thumbString", "isDefault", "isBridge", "isToggled" }),

            new Family("Referenced assets",
                "Asset paths, resolved through EngineIntegration at load. A path that no longer resolves is the one "
                + "failure this format can suffer without any number being wrong, which is why ShaderInventoryGate "
                + "case 13 loads every path in every shipped library and requires each material it reaches to be on "
                + "URP Lit. isStatic marks the placed instances batchable; the two combined-mesh flags ask the maker "
                + "to weld a run of them into one mesh and one collider.",
                new[]
                {
                    "edgeObjectString", "isMaterialOverriden", "edgeMaterial1String", "edgeMaterial2String",
                    "isCombinedMesh", "isCombinedMeshCollider", "isStatic"
                }),

            new Family("Where along the road: fractions of the segment, and metres between repeats",
                "startTime and endTime are fractions (0..1) of the spline segment the run occupies. meterSep IS a real "
                + "spacing in metres and is the one number in this family a published standard can govern - lamp "
                + "spacing near 3x mounting height, MUTCD channelizing-device spacing, the 12.2 m broken-line cycle - "
                + "which is why RM-86 audits it per file rather than blanket-marking it authored. isSingle switches the "
                + "entry from a repeating run to ONE object at singlePosition, and meterSep then governs nothing, so "
                + "the audit records it as a fallback rather than a spacing. singleOnlyBridgePercent places that single "
                + "object as a percentage along a bridge span.",
                new[]
                {
                    "startTime", "endTime", "meterSep", "isSingle", "singlePosition", "singleOnlyBridgePercent",
                    "isStartMatchingRoadDefinition", "startMatchRoadDef"
                }),

            new Family("Which side, and how far off the road: metres",
                "subType picks left, right or both. horizontalSep is the lateral offset from the road centre in metres "
                + "and verticalRaise the height above the road surface in metres; each has an optional AnimationCurve "
                + "that varies it along the run. These are PLACEMENTS in the cross-section, not carriageway "
                + "dimensions - the carriageway is owned by RM-31/RM-59/RM-96 and must never be sized from them. "
                + "isMatchingTerrain drops each instance to the ground instead of following the road surface.",
                new[]
                {
                    "subType", "horizontalSep", "horizontalCurve", "verticalRaise", "verticalCurve", "isMatchingTerrain"
                }),

            new Family("Rotation: degrees, Euler",
                "customRotation is applied on top of the alignment. isRotationAligning turns each instance to the "
                + "road's heading; isOncomingRotation faces it into oncoming traffic (a sign the driver must read); the "
                + "three lock flags hold an axis at its authored value so a lamp head stays level over a banked road.",
                new[]
                {
                    "customRotation", "isRotationAligning", "isOncomingRotation", "isXRotationLocked",
                    "isYRotationLocked", "isZRotationLocked"
                }),

            new Family("Explicit per-object placement",
                "Positions and Euler rotations of individually placed instances, in metres and degrees, used instead "
                + "of the spacing above when the author has hand-placed the run. Written as element lists, so a preset "
                + "with fifty hand-placed posts carries fifty entries - the only unbounded part of the format.",
                new[] { "edgeObjectLocations", "edgeObjectRotations" })
        };

        private static readonly Family[] _section =
        {
            new Family("Format stamp", NoteStamp, new[] { "formatVersion" }),

            new Family("Identity and presentation",
                "Naming and wizard-grid presentation only; isToggled is the inspector fold-out state.",
                new[] { "objectName", "displayName", "desc", "thumbString", "isDefault", "isBridge", "isToggled" }),

            new Family("Referenced assets",
                "The mesh swept along the spline, its two end-cap meshes, and up to two override materials. The two "
                + "EndCap*String entries name the objects placed AT the ends (a bridge pier, a barrier terminal) as "
                + "distinct from the caps that close the swept mesh itself.",
                new[]
                {
                    "CurrentSplinationString", "CurrentSplinationCap1String", "CurrentSplinationCap2String",
                    "isMaterialOverriden", "SplinatedMaterial1String", "SplinatedMaterial2String",
                    "EndCapStartString", "EndCapEndString", "isStatic"
                }),

            new Family("Where along the road: fractions of the segment",
                "StartTime and EndTime are fractions (0..1) of the spline segment the extrusion covers - capitalised "
                + "here and lower-case on the edge-object record, which is the format's one naming inconsistency and is "
                + "kept because renaming an element breaks every file already written. isMatchingRoadDefinition steps "
                + "the extrusion at the road's own vertex spacing so the swept mesh cannot cut a corner the road "
                + "rounds; isExactSplination lays exactly one copy of the mesh per step rather than stretching it; "
                + "isTrimStart/isTrimEnd pull the run back off a junction.",
                new[]
                {
                    "StartTime", "EndTime", "isTrimStart", "isTrimEnd", "isMatchingRoadDefinition",
                    "isMatchingRoadIncrements", "isExactSplination", "isStraightLineMatchingStartEnd"
                }),

            new Family("Lateral and vertical placement: metres",
                "horizontalSep is the lateral offset from the road centre and verticalRaise the height above the road "
                + "surface, both in metres, each with an optional curve along the run; RoadRaise lifts the road "
                + "surface itself where the section demands it (a bridge deck). These are the numbers the RM-86 audit "
                + "records as AUTHORED placements: a railing offset of 8.5 m on the 2-lane bridge preset is matched to "
                + "the deck edge modelled into BridgeBase9On2Lanes.FBX, not derived from a lane width, and "
                + "recomputing it from a carriageway would slide the railing off the deck.",
                new[] { "horizontalSep", "horizontalCurve", "verticalRaise", "verticalCurve", "RoadRaise", "isMatchingTerrain" }),

            new Family("Rotation and sweep axis",
                "CustomRotation is applied to each swept copy in degrees; Axis says which of the mesh's own axes runs "
                + "along the road (0/1/2), isFlippedRotation turns the section to face the other way, and "
                + "isNotCenterMode measures the offsets from the road edge rather than its centre line.",
                new[] { "CustomRotation", "isFlippedRotation", "Axis", "isNotCenterMode" }),

            new Family("Vertical cutoff: metres",
                "Clips the swept mesh at a height so an arch rib or pier stops at the deck instead of growing through "
                + "it. VerticalCutoff is that height in metres and VerticalMeshCutoffOffset the tolerance the cut is "
                + "made at - a solver figure, not a dimension. The Arch12m/24m/48m presets are byte-identical in this "
                + "family and differ only in the mesh they name, which is the evidence that arch span and rise live in "
                + "the .fbx and not in this file.",
                new[]
                {
                    "VerticalCutoff", "isVerticalCutoff", "isVerticalCutoffDownwards", "isVerticalMeshCutoffOppositeDir",
                    "VerticalMeshCutoffOffset", "isVerticalCutoffMatchingZero"
                }),

            new Family("Stretch and UV repeat",
                "Whether one copy of the mesh is stretched to fill the run instead of repeated, and how its texture "
                + "coordinates follow. stretchUVThreshold is the fraction of a UV tile below which the stretch is "
                + "accepted rather than a new copy started; RepeatUVType selects the tiling mode. Appearance and "
                + "tolerance, no dimensions.",
                new[] { "isStretched", "isStretchedLocOffset", "isStretchedSize", "stretchUVThreshold", "RepeatUVType" }),

            new Family("Solver tolerances",
                "Meshing epsilons in metres: MinMaxMod is the margin the extruder allows when matching a vertex to the "
                + "cross-section band, VertexMatchingPrecision the distance below which two vertices are treated as "
                + "the same point and welded. Real units, but no real-world standard governs them - they are properties "
                + "of the mesher, and the audit marks them AUTHORED tuning for that reason rather than hunting for a "
                + "published figure that does not exist.",
                new[] { "MinMaxMod", "VertexMatchingPrecision" }),

            new Family("Measured mesh extents: metres, computed not authored",
                "The bounding extents of the referenced mesh, cached into the file so the extruder need not load the "
                + ".fbx to size a preview. They are MEASUREMENTS of an asset, so a mismatch between them and the mesh "
                + "is a stale cache rather than a wrong dimension, and correcting them against a published section "
                + "would be meaningless. -1 in all six is the unset sentinel the maker recomputes from.",
                new[] { "mMaxX", "mMinX", "mMaxY", "mMinY", "mMaxZ", "mMinZ" }),

            new Family("Collision",
                "The collider built for the swept run: CollisionType picks none, box, mesh or the three-point triangle "
                + "form, and isCollisionConvex/isCollisionTrigger set the mesh collider's own flags. The box offset and "
                + "size and the seven Box/Tri corner points are in metres, but with isSimpleCollisionAutomatic true - "
                + "the shipped default - they are COMPUTED from the referenced mesh, which is why the audit records "
                + "them as collision proxies with the real-world figure named only for context: a proxy corrected to a "
                + "rail-top specification would no longer match the mesh it is meant to wrap.",
                new[]
                {
                    "CollisionType", "isCollisionConvex", "isSimpleCollisionAutomatic", "isCollisionTrigger",
                    "boxColliderOffset", "boxColliderSize", "isBoxColliderFlippedOnX", "isBoxColliderFlippedOnZ",
                    "CollisionBoxBL", "CollisionBoxBR", "CollisionBoxTL", "CollisionBoxTR",
                    "CollisionTriBL", "CollisionTriBR", "CollisionTriT"
                }),

            new Family("End caps: metres and degrees",
                "CapHeightOffset1/2 raise or lower the meshes that close the two ends of the swept run, in metres. The "
                + "four EndCapCustom* vectors offset and rotate the objects placed at the ends (positions in metres, "
                + "rotations in degrees); isEndCapCustomMatchStart makes the end copy the start rather than carrying "
                + "its own numbers, and isEndObjectsMatchingGround drops them to the terrain.",
                new[]
                {
                    "CapHeightOffset1", "CapHeightOffset2", "isEndCapCustomMatchStart", "EndCapCustomOffsetStart",
                    "EndCapCustomOffsetEnd", "EndCapCustomRotOffsetStart", "EndCapCustomRotOffsetEnd",
                    "isEndObjectsMatchingGround"
                }),

            new Family("Down-turns at the ends: metres",
                "A guard rail is turned down and buried at its terminal so an errant vehicle rides over it instead of "
                + "spearing into a blunt end - a real roadside-safety requirement, and the AASHTO Roadside Design Guide "
                + "treatment these flags exist to reproduce. isStartDown/isEndDown turn the run down; the Override "
                + "pair replaces the computed drop with StartTypeDownOverride/EndTypeDownOverride metres.",
                new[]
                {
                    "isStartDown", "isStartTypeDownOverriden", "StartTypeDownOverride",
                    "isEndDown", "isEndTypeDownOverriden", "EndTypeDownOverride"
                })
        };


        // ------------------------------------------------------------------ lookup ------

        /// <summary> The three root element names, in the order the schema describes them. </summary>
        public static string[] Roots()
        {
            return new[] { RaoFormat.RootWizard, RaoFormat.RootSplinatedSection, RaoFormat.RootEdgeObject };
        }


        /// <summary> The families describing one root, or <c>null</c> for a name that is not a <c>.rao</c> root. </summary>
        public static Family[] FamiliesFor(string _root)
        {
            if (_root == RaoFormat.RootWizard) { return _wizard; }
            if (_root == RaoFormat.RootSplinatedSection) { return _section; }
            if (_root == RaoFormat.RootEdgeObject) { return _edgeObject; }
            return null;
        }


        /// <summary> The record class written under one root, so a caller can reflect on it and hold this schema to
        /// what the serialiser will actually emit. Returns <c>null</c> for a name that is not a root. </summary>
        public static System.Type RecordTypeFor(string _root)
        {
            if (_root == RaoFormat.RootWizard) { return typeof(WizardObject.WizardObjectLibrary); }
            if (_root == RaoFormat.RootSplinatedSection)
            {
                return typeof(Splination.SplinatedMeshMaker.SplinatedMeshLibraryMaker);
            }
            if (_root == RaoFormat.RootEdgeObject)
            {
                return typeof(EdgeObjects.EdgeObjectMaker.EdgeObjectLibraryMaker);
            }
            return null;
        }


        // ------------------------------------------------------------------ the document ------

        /// <summary> The schema as text, generated from the types and the families above. This is what RM-105 owes as
        /// "a documented schema": it is produced from the code every time it is asked for, so it cannot describe a
        /// format the tool no longer writes. The manual embeds it, and <c>RaoLibraryGate</c> checks it. </summary>
        public static string Describe()
        {
            StringBuilder builder = new StringBuilder(8192);

            builder.Append("THE .rao LIBRARY FORMAT\n");
            builder.Append("=======================\n\n");
            builder.Append("A .rao file is one or more UTF-8 XML documents concatenated end to end, each separated by a\n");
            builder.Append("line of its own reading:  ").Append(RoadUtility.FileSepString.Trim()).Append('\n');
            builder.Append("Files written before the tool was renamed carry ")
                   .Append(RoadUtility.FileSepStringLegacy.Trim())
                   .Append(" instead; the reader accepts\n")
                   .Append("both, and both line endings of each, so a library saved by any version still opens.\n\n");
            builder.Append("A single-record file is a legal .rao with one record in it, which is why every EOM*.rao and\n");
            builder.Append("ESO*.rao opens as plain XML while the group presets under Groups/ do not. Records are read one\n");
            builder.Append("at a time and identified by their ROOT ELEMENT NAME - never by file name, never by trying a\n");
            builder.Append("deserialiser and treating failure as a type test. A record of another kind is stepped over in\n");
            builder.Append("silence; a record of the right kind that will not parse is REFUSED, naming the file, the record\n");
            builder.Append("number and the cause, and nothing half-loaded reaches the scene.\n\n");
            builder.Append("Every record carries <formatVersion>. Absent or 0 means a legacy file and is read as version ")
                   .Append(RaoFormat.LegacyVersion).Append(";\n")
                   .Append("this tool writes version ").Append(RaoFormat.CurrentVersion)
                   .Append(" and refuses anything higher by number rather than guessing at fields it\n")
                   .Append("has never seen.\n\n");
            builder.Append("Units: distances in METRES, rotations in DEGREES (Euler), and any field named *Time is a\n");
            builder.Append("FRACTION of the spline segment (0 at its start, 1 at its end) - not a duration.\n");

            string[] roots = Roots();
            for (int r = 0; r < roots.Length; r++)
            {
                string root = roots[r];
                System.Type recordType = RecordTypeFor(root);
                Family[] families = FamiliesFor(root);
                string[] elements = ElementNames(recordType);

                builder.Append("\n\n<").Append(root).Append(">\n");
                builder.Append(new string('-', root.Length + 2)).Append('\n');
                builder.Append("Written by ").Append(recordType.FullName.Replace('+', '.')).Append(" - ")
                       .Append(elements.Length).Append(" elements in ").Append(families.Length)
                       .Append(" families.\n");

                for (int f = 0; f < families.Length; f++)
                {
                    builder.Append("\n  ").Append(families[f].name).Append('\n');
                    builder.Append("    ").Append(families[f].note).Append('\n');
                    string[] members = families[f].members;
                    for (int m = 0; m < members.Length; m++)
                    {
                        builder.Append(m == 0 ? "    elements: " : ", ");
                        builder.Append(members[m]);
                    }
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }
    }
}
