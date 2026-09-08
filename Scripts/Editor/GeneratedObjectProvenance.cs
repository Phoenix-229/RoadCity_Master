// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using RoadCityMaster.City;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> How a row's pattern is compared against a GameObject's name. The tool names its generated output three
    /// ways and no more, which is why there are three rules here rather than a regular expression: a fixed string, a
    /// fixed prefix followed by an index (and sometimes a fixed tail after that index), and a fixed suffix on a name
    /// whose head is the OWNING object's own name. A name matching none of the three was not written by this tool, and
    /// the panel says nothing about it. </summary>
    public enum GeneratedNameRule
    {
        /// <summary> The whole name, verbatim: "MainMeshes", "outer", "StopSignRR". </summary>
        Exact,
        /// <summary> A prefix, digits, then the row's tail if it has one: "RoadCut7", "Node3Ignore". </summary>
        PrefixThenIndex,
        /// <summary> A suffix on the owner's own name: "Inter3-Lane0", whose head is the intersection. </summary>
        Suffix
    }

    /// <summary> What the next build does to the object. This is the fact an author most needs and the one the hierarchy
    /// window cannot tell them; every answer below is read off the code that creates the object rather than assumed.
    /// </summary>
    public enum RebuildDisposition
    {
        /// <summary> Destroyed and built again. A hand edit to it is gone. </summary>
        Regenerated,
        /// <summary> The object is found and kept, but its mesh and its position are written over. </summary>
        MeshRewritten,
        /// <summary> Scratch: destroyed before the build returns. Seeing one means a build stopped part way. </summary>
        Transient,
        /// <summary> Made once and never remade. It is INPUT to the build, and editing it is the point. </summary>
        AuthorOwned
    }

    /// <summary> One name the tool gives its own output, and the three sentences an author needs about it: what the
    /// object is, what produced it, and what the next build does to it. </summary>
    public sealed class GeneratedObjectRow
    {
        public readonly GeneratedNameRule rule;
        public readonly string pattern;   // the fixed part: whole name, prefix, or suffix
        public readonly string tail;      // PrefixThenIndex only: what must follow the digits; usually empty
        public readonly string title;     // what it is, in two or three words
        public readonly string whatItIs;  // one sentence, in the author's language
        public readonly string producedBy;// the class, and the method where it is worth naming
        public readonly RebuildDisposition disposition;

        public GeneratedObjectRow(GeneratedNameRule rule, string pattern, string tail, string title,
            string whatItIs, string producedBy, RebuildDisposition disposition)
        {
            this.rule = rule; this.pattern = pattern; this.tail = tail ?? string.Empty;
            this.title = title; this.whatItIs = whatItIs; this.producedBy = producedBy;
            this.disposition = disposition;
        }

        /// <summary> Whether this row names the given object. Pure: it reads a string and touches no scene. </summary>
        public bool Matches(string name)
        {
            if (string.IsNullOrEmpty(name) || pattern.Length < 1) { return false; }

            if (rule == GeneratedNameRule.Exact) { return name == pattern; }

            if (rule == GeneratedNameRule.Suffix)
            {
                return name.Length > pattern.Length && name.EndsWith(pattern, System.StringComparison.Ordinal);
            }

            // PrefixThenIndex: the prefix, then at least one digit, then exactly the tail. Requiring a digit is what
            // keeps "RoadMesh" out of the "Road" row and "Intersections" out of the "Inter" row without a second list.
            if (!name.StartsWith(pattern, System.StringComparison.Ordinal)) { return false; }
            int end = name.Length - tail.Length;
            if (end <= pattern.Length) { return false; }
            if (tail.Length > 0 && !name.EndsWith(tail, System.StringComparison.Ordinal)) { return false; }
            for (int i = pattern.Length; i < end; i++)
            {
                if (!char.IsDigit(name[i])) { return false; }
            }
            return true;
        }
    }

    /// <summary> The answer about one object, ready to draw. <see cref="isKnown"/> false means the tool did not name this
    /// object and the panel must draw nothing at all -- the header hook fires for every object in the project, so
    /// silence on someone else's GameObject is a requirement, not a nicety. </summary>
    public struct GeneratedObjectView
    {
        public readonly bool isKnown;
        public readonly string title;
        public readonly string whatItIs;
        public readonly string producedBy;
        public readonly RebuildDisposition disposition;
        /// <summary> Set when the object's own name is not in the table but a GENERATED ancestor is: the name of that
        /// ancestor. The object is then generated output too, and goes when the ancestor goes. Empty otherwise. </summary>
        public readonly string insideGenerated;

        public GeneratedObjectView(bool isKnown, string title, string whatItIs, string producedBy,
            RebuildDisposition disposition, string insideGenerated)
        {
            this.isKnown = isKnown; this.title = title; this.whatItIs = whatItIs; this.producedBy = producedBy;
            this.disposition = disposition; this.insideGenerated = insideGenerated ?? string.Empty;
        }

        /// <summary> Not ours. Every field is empty and the panel draws nothing. </summary>
        public static GeneratedObjectView Unknown
        {
            get
            {
                return new GeneratedObjectView(false, string.Empty, string.Empty, string.Empty,
                    RebuildDisposition.AuthorOwned, string.Empty);
            }
        }
    }

    /// <summary> B-11's resolver: given the NAME of a GameObject, what the road tool made it for, what produced it, and
    /// what the next build does to it. Selecting a generated child used to show a bare Transform and MeshRenderer with
    /// nothing saying what it was, which road or intersection owned it, or that editing it by hand would be thrown away
    /// on the next build. This answers all four, and <see cref="GeneratedObjectPanel"/> draws the answer.
    ///
    /// It is deliberately PURE -- every entry point but <see cref="TryFindOwner"/> takes a string and touches no scene --
    /// because the panel is drawn from an inspector header hook that fires for every object an author selects anywhere in
    /// the project. A resolver that walked the scene to decide whether to say anything would pay for that walk on objects
    /// that have nothing to do with this tool.
    ///
    /// Every name below was read off the code that creates it. Three names that LOOK like generated output are absent on
    /// purpose: <c>col2</c> (IntersectionObjects) and the four <c>temp22_*</c> debug cubes sit in commented-out or
    /// never-called code and are never created, and <c>TempTrafficLight</c> is renamed to its corner's TrafficLight name
    /// before it is ever parented, so no author can select one. Describing any of them would be describing nothing.
    /// </summary>
    public static class GeneratedObjectProvenance
    {
        private const string RoadBuilder = "RoadConstructorBufferMaker, the road's mesh builder";
        private const string InterOuter = "Intersections.CreateIntersectionMeshOuter";
        private const string InterCombine = "RoadConstructorBufferMaker.MeshSetup2CombineIntersections";
        private const string SignBuilder = "IntersectionObjects.CreateStopSignsAllWay";
        private const string SignalBuilder = "IntersectionObjects.CreateTrafficLightBases";
        private const string PoleBuilder = "IntersectionObjects.ProcessPole";
        private const string Splinated = "SplinatedMeshMaker, from the node's splinated object";
        private const string Scaffolding = "the tool's own create commands";

        private static List<GeneratedObjectRow> _rows;

        private static void One(List<GeneratedObjectRow> r, string name, string title, string what, string by,
            RebuildDisposition d)
        {
            r.Add(new GeneratedObjectRow(GeneratedNameRule.Exact, name, string.Empty, title, what, by, d));
        }

        private static void Indexed(List<GeneratedObjectRow> r, string prefix, string tail, string title, string what,
            string by, RebuildDisposition d)
        {
            r.Add(new GeneratedObjectRow(GeneratedNameRule.PrefixThenIndex, prefix, tail, title, what, by, d));
        }

        private static void Suffixed(List<GeneratedObjectRow> r, string suffix, string title, string what, string by,
            RebuildDisposition d)
        {
            r.Add(new GeneratedObjectRow(GeneratedNameRule.Suffix, suffix, string.Empty, title, what, by, d));
        }

        /// <summary> One description over several names, so a family of eight signal or sign objects is one sentence
        /// rather than eight copies of one sentence. </summary>
        private static void Family(List<GeneratedObjectRow> r, string[] names, string title, string what, string by,
            RebuildDisposition d)
        {
            for (int i = 0; i < names.Length; i++) { One(r, names[i], title, what, by, d); }
        }

        /// <summary> The name table, built once, grouped by WHERE the objects live -- under a road, under an
        /// intersection, under the system -- because that is the order someone maintaining it reads the scene in. It is
        /// deliberately not sorted by rule: <see cref="For"/> takes the first match, so grouping this way means a broad
        /// prefix or suffix rule can sit above an exact name and shadow it. InspectorConsistencyGate case 12 probes every
        /// row with its own name and fails if any row does not answer for itself, so the readable order is kept and the
        /// hazard it carries is checked rather than assumed away. </summary>
        public static IList<GeneratedObjectRow> Rows()
        {
            if (_rows != null) { return _rows; }
            List<GeneratedObjectRow> r = new List<GeneratedObjectRow>();

            // --- Under a road: the mesh tree. InitGameObjects destroys MainMeshes and everything in it, then builds it
            // again, so nothing here survives a build.
            One(r, "MainMeshes", "Road mesh container",
                "Every mesh this road builds hangs under here -- the carriageway, both shoulders, and the plates that "
                + "carry the markings. It holds no mesh of its own.", RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "RoadMesh", "Carriageway surface",
                "The travelled way, and the object the road markings are drawn on. Its asphalt is the Pavement child "
                + "below it, so the two can carry different materials.", RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "Pavement", "Carriageway asphalt",
                "The asphalt of the travelled way, split out from the markings on its parent so each can carry its own "
                + "material.", RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "ShoulderR", "Right shoulder",
                "The strip outside the running lanes on the road's right, built to the shoulder width and cross-fall "
                + "set on the road.", RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "ShoulderL", "Left shoulder",
                "The strip outside the running lanes on the road's left, built to the shoulder width and cross-fall "
                + "set on the road.", RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "RoadConnectionBase", "Connection surface",
                "The paved mouth where this road meets another. It carries the collider traffic drives on.",
                RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "RoadConnectionMarker", "Connection markings",
                "The markings across the mouth where this road meets another, drawn as their own plate.",
                RoadBuilder, RebuildDisposition.Regenerated);
            Indexed(r, "RoadCut", string.Empty, "Carriageway cut section",
                "One length of carriageway between two road cuts, built as its own object so that stretch can carry a "
                + "different material from the rest of the road.", RoadBuilder, RebuildDisposition.Regenerated);
            Indexed(r, "SCutL", string.Empty, "Left shoulder cut section",
                "One length of the left shoulder between two shoulder cuts, built as its own object so that stretch can "
                + "carry its own material.", RoadBuilder, RebuildDisposition.Regenerated);
            Indexed(r, "SCutR", string.Empty, "Right shoulder cut section",
                "One length of the right shoulder between two shoulder cuts, built as its own object so that stretch can "
                + "carry its own material.", RoadBuilder, RebuildDisposition.Regenerated);
            Indexed(r, "Markers", string.Empty, "Marking plate for a cut",
                "The markings belonging to the cut section it sits under, drawn as a separate plate a hair above the "
                + "surface so the two do not fight for the same pixels.", RoadBuilder, RebuildDisposition.Regenerated);

            // The seven junction staging containers and the plates assembled inside them. All seven are destroyed at the
            // end of MeshSetup2, after the plates have been combined onto the intersections, so one of these left in the
            // hierarchy is evidence that a build stopped part way rather than something to keep.
            Family(r, new string[] { "MeshiLanes", "MeshiLanes0", "MeshiLanes1", "MeshiLanes2", "MeshiLanes3",
                "MeshiMainPlates", "MeshiMarkerPlates" }, "Junction staging container",
                "Scratch parent the junction plates are assembled under. The build destroys all seven of these once the "
                + "plates have been combined onto the intersections, so one still here means a build stopped part way.",
                RoadBuilder, RebuildDisposition.Transient);
            Family(r, new string[] { "Lane0F", "Lane0B", "Lane1F", "Lane1B", "Lane2F", "Lane2B", "Lane3F", "Lane3B",
                "LaneD1F", "LaneD1B", "LaneD2F", "LaneD2B", "LaneD3F", "LaneD3B", "LaneDA1B", "LaneDA2F", "LaneDA2B",
                "LaneDAR1F", "LaneDAR2F", "MainPlateB", "MainPlateBM", "MainPlateFM" }, "Junction staging plate",
                "One road's contribution to one layer of a junction -- a lane's markings, or a piece of its surface. "
                + "They are merged per intersection into the combined plate that ends up under the intersection, and "
                + "then destroyed with their staging container.", RoadBuilder, RebuildDisposition.Transient);

            // --- Under an intersection: the junction's own geometry, its signs and its signals.
            One(r, "outer", "Junction apron",
                "The apron that closes the gap between the junction's four corners and the roads that meet there. The "
                + "object is found and kept between builds; its mesh and its position are written again every time.",
                InterOuter, RebuildDisposition.MeshRewritten);
            One(r, "tCenter", "Junction centre patch",
                "The middle of the junction, filled in. It is destroyed the moment the junction's carriageway plate is "
                + "built over it, and left standing when that plate is not built -- so finding one means the plates were "
                + "not reached.", RoadBuilder, RebuildDisposition.Regenerated);
            One(r, "CenterMarkers", "Junction centre markings",
                "The markings across the middle of the junction, drawn as their own plate and set not to cast shadows.",
                RoadBuilder, RebuildDisposition.Regenerated);
            Family(r, new string[] { "StopSignRR", "StopSignRL", "StopSignLL", "StopSignLR" }, "Stop sign",
                "The stop sign at one corner of the junction, named for the pair of approach sides it stands between. "
                + "All four are destroyed and made again whenever the junction's signs or signals are rebuilt.",
                SignBuilder, RebuildDisposition.Regenerated);
            Family(r, new string[] { "TrafficLightRR", "TrafficLightRL", "TrafficLightLL", "TrafficLightLR" },
                "Signal pole and mast",
                "The signal pole at one corner of the junction, with its mast arm reaching across the approach. Its "
                + "signal heads, its yield sign and any street light hang under it and go with it.",
                SignalBuilder, RebuildDisposition.Regenerated);
            One(r, "LightLeft", "Left-turn signal head",
                "The head over the left-turn movement on this mast arm.", PoleBuilder, RebuildDisposition.Regenerated);
            One(r, "LightRight", "Right-turn signal head",
                "The head over the right-turn movement on this mast arm.", PoleBuilder, RebuildDisposition.Regenerated);
            Indexed(r, "Light", string.Empty, "Through-lane signal head",
                "One head over one through lane; there is one for each lane the mast arm covers.",
                PoleBuilder, RebuildDisposition.Regenerated);
            One(r, "SignYieldOnGreen", "Yield-on-green sign",
                "The sign hung beside a permitted turn head, telling the turning movement to give way on a green ball.",
                PoleBuilder, RebuildDisposition.Regenerated);
            One(r, "StreetLight", "Street light luminaire",
                "The luminaire lifted to the top of the signal mast. It is the mast's own modelled height that places "
                + "it, so a taller replacement mesh carries the light higher with it.",
                "IntersectionObjects.CreateTrafficLight", RebuildDisposition.Regenerated);

            // The combined plates the road builder parents onto each intersection. The HEAD of the name is the
            // intersection's own name, which is why these match by suffix: "Inter3-Lane0" belongs to Inter3.
            Suffixed(r, "-TiledExt", "Junction carriageway plate",
                "The surface traffic drives on through the junction: every approaching road's contribution to it, merged "
                + "into one mesh with its texture tiled. This is the plate that carries the mesh collider and the road's "
                + "physics material.", InterCombine, RebuildDisposition.Regenerated);
            Suffixed(r, "-StretchExt", "Junction stretched plate",
                "The same junction surface with its texture stretched across the junction rather than tiled. It is lifted "
                + "a centimetre above the tiled plate so the two do not fight for the same pixels.",
                InterCombine, RebuildDisposition.Regenerated);
            string[] layers = new string[] { "-Lane0", "-Lane1", "-Lane2", "-Lane3", "-LaneD1", "-LaneD2", "-LaneD3",
                "-LaneDA1", "-LaneDA2", "-LaneDAR2" };
            for (int i = 0; i < layers.Length; i++)
            {
                Suffixed(r, layers[i], "Junction marking plate",
                    "The " + layers[i].Substring(1) + " marking layer of this junction: every approaching road's "
                    + "contribution to it, merged into one mesh and parented onto the intersection. In the layer name a "
                    + "D is a disabled lane, DA its outer edge, and the trailing number is the lane.",
                    InterCombine, RebuildDisposition.Regenerated);
            }

            // --- Under a node: splinated objects. The author names these, so only the two names the TOOL chooses can be
            // tabled; anything else is caught by the generated-ancestor fallback in ForTransform.
            One(r, "ExtrudedMesh", "Splinated mesh",
                "A mesh extruded along the road's alignment -- a barrier, a kerb, a rail. This is the name it takes when "
                + "the splinated object was left unnamed.", Splinated, RebuildDisposition.Regenerated);
            One(r, "Primitive", "Splinated box collider",
                "The box collider for one node's length of a splinated mesh, made when that object is set to box "
                + "collision rather than mesh collision.", Splinated, RebuildDisposition.Regenerated);

            // --- The scaffolding the tool creates ONCE. These are input to the build, not output from it: an author
            // moves a node, renames a road, sets a class. Saying "a rebuild overwrites this" about one of them would be
            // false, which is why the disposition is a four-way answer and not a flag.
            Indexed(r, "RoadCityMasterSystem", string.Empty, "Road system",
                "The root every road, spline and intersection of one network lives under. It has its own inspector.",
                Scaffolding, RebuildDisposition.AuthorOwned);
            Indexed(r, "Road", string.Empty, "Road",
                "One road. Its class, cross-section, speed and alignment are authored on it, and the MainMeshes tree "
                + "under it is what gets built from them.", Scaffolding, RebuildDisposition.AuthorOwned);
            One(r, "Spline", "Road alignment",
                "The road's alignment: the nodes under it are its horizontal and vertical geometry, and every mesh the "
                + "road builds is measured along it.", Scaffolding, RebuildDisposition.AuthorOwned);
            Indexed(r, "Node", "Ignore", "Skipped alignment node",
                "An alignment node the road is told to skip. It stays in the hierarchy so it can be switched back on.",
                Scaffolding, RebuildDisposition.AuthorOwned);
            Indexed(r, "Node", string.Empty, "Alignment node",
                "One point on the road's alignment, with its own elevation, its bridge and tunnel settings and the "
                + "splinated and edge objects attached at it.", Scaffolding, RebuildDisposition.AuthorOwned);
            One(r, "Intersections", "Intersection container",
                "The container every intersection in this network is parented to. It holds nothing of its own.",
                Scaffolding, RebuildDisposition.AuthorOwned);
            Indexed(r, "Inter", string.Empty, "Intersection",
                "A junction where two roads cross. Its type, its signs and its signals are authored on it; the plates, "
                + "apron, signs and poles under it are built from that.", Scaffolding, RebuildDisposition.AuthorOwned);

            _rows = r;
            return _rows;
        }

        /// <summary> How many names the table carries, for the gate. </summary>
        public static int RowCount { get { return Rows().Count; } }

        /// <summary> The answer for one NAME. Pure: no scene is read, so the header hook can call it for every object an
        /// author selects anywhere in the project and pay only a string comparison on the ones that are not ours.
        /// </summary>
        public static GeneratedObjectView For(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) { return GeneratedObjectView.Unknown; }

            IList<GeneratedObjectRow> rows = Rows();
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].Matches(objectName)) { continue; }
                return new GeneratedObjectView(true, rows[i].title, rows[i].whatItIs, rows[i].producedBy,
                    rows[i].disposition, string.Empty);
            }
            return ForCityName(objectName);
        }

        /// <summary> The city half. The names are not tabled above because CityHierarchy already owns them: the root, the
        /// five branches and the "Kind[element]#ordinal" form are its vocabulary, and re-typing them here is the
        /// duplication that lets two lists disagree. The operators that will EMIT the element names are B-15's; the
        /// branch answers below are what CityHierarchy.EnsureBranch already does, which is find-or-create and never
        /// destroy. </summary>
        private static GeneratedObjectView ForCityName(string objectName)
        {
            if (objectName == CityHierarchy.RootName)
            {
                return new GeneratedObjectView(true, "City root",
                    "The root the five city branches live under. It holds nothing of its own.",
                    "CityHierarchy.EnsureBranch", RebuildDisposition.AuthorOwned, string.Empty);
            }

            IList<CityBranch> branches = CityHierarchy.All();
            for (int i = 0; i < branches.Count; i++)
            {
                if (objectName != CityHierarchy.BranchName(branches[i])) { continue; }
                return new GeneratedObjectView(true, "City branch: " + CityHierarchy.BranchName(branches[i]),
                    CityHierarchy.BranchPurpose(branches[i]), "CityHierarchy.EnsureBranch",
                    RebuildDisposition.AuthorOwned, string.Empty);
            }

            string elementId;
            if (!CityHierarchy.TryParseName(objectName, out elementId)) { return GeneratedObjectView.Unknown; }

            // Which kind it is. The loop that read this off the enum used to sit here; C-11's city inspector needed the
            // same answer, so it moved to CityHierarchy.TryParseKind -- beside the NameFor that writes the name, and now
            // with one owner instead of two copies free to drift after a member is renamed.
            GeneratedKind kind;
            if (!CityHierarchy.TryParseKind(objectName, out kind)) { return GeneratedObjectView.Unknown; }

            return new GeneratedObjectView(true, "City " + kind.ToString().ToLower(),
                "Built for the city element " + elementId + ", and filed under the "
                + CityHierarchy.BranchName(CityHierarchy.BranchFor(kind)) + " branch. The name is the tool's own, "
                + "which is how it tells its output apart from a child you added yourself.",
                "the city operator for " + kind.ToString().ToLower() + ", named by CityHierarchy.NameFor",
                RebuildDisposition.Regenerated, string.Empty);
        }

        /// <summary> The consequence of the disposition, in one sentence an author can act on. This is the whole point of
        /// the panel: "will my hand edit survive the next build" is the question the hierarchy could not answer. </summary>
        public static string DispositionSentence(RebuildDisposition disposition)
        {
            if (disposition == RebuildDisposition.Regenerated)
            {
                return "The next build DESTROYS this object and makes it again. A mesh, a material, a position or a "
                    + "component you set on it by hand is gone then. Change the road or the intersection that owns it.";
            }
            if (disposition == RebuildDisposition.MeshRewritten)
            {
                return "The next build KEEPS this object but writes its mesh and its position again. A component you add "
                    + "survives; a mesh or a placement you set by hand does not.";
            }
            if (disposition == RebuildDisposition.Transient)
            {
                return "This is scratch the build destroys before it finishes. Finding one still here means a build "
                    + "stopped part way -- rebuild the road rather than deleting it by hand.";
            }
            return "The tool made this once and never remakes it. It is INPUT to the build: editing it is how you change "
                + "what gets built, and it will not be overwritten.";
        }

        /// <summary> The answer for an object IN THE SCENE: its own name first, then the nearest generated ancestor. The
        /// fallback is not padding -- edge objects take the author's own prefab name (EdgeObjectMaker) and a splinated
        /// object can be called anything (SplinatedMeshMaker's objectName), so those names can never be tabled, yet they
        /// still go when the parent that owns them goes, and an author is entitled to know that. The walk STOPS at the
        /// first author-owned ancestor: a child you parented to your own node is yours, and the panel stays silent.
        /// </summary>
        public static GeneratedObjectView ForTransform(Transform t)
        {
            if (t == null) { return GeneratedObjectView.Unknown; }

            GeneratedObjectView own = For(t.name);
            if (own.isKnown) { return own; }

            Transform walk = t.parent;
            while (walk != null)
            {
                GeneratedObjectView up = For(walk.name);
                if (up.isKnown)
                {
                    if (up.disposition == RebuildDisposition.AuthorOwned) { return GeneratedObjectView.Unknown; }
                    return new GeneratedObjectView(true, "Inside generated output",
                        "The tool did not name this object, so it is either yours or one you named yourself -- but it "
                        + "sits under " + walk.name + ", which the build owns.",
                        up.producedBy, up.disposition, walk.name);
                }
                walk = walk.parent;
            }
            return GeneratedObjectView.Unknown;
        }

        /// <summary> The nearest thing up the chain an author can actually edit, and the word for it. The intersection is
        /// tested before the road because the combined junction plates and the centre markers are PRODUCED by the road's
        /// mesh builder yet PARENTED to the intersection -- selecting the intersection is what lets an author change them,
        /// which is why the panel names the producer separately from the owner.
        ///
        /// A city is recognised by its COMPONENT first. <c>CitySettlementFactory.CreateCity</c> names the root from an
        /// author-supplied name through UniqueChildName, so a city called "Riverport" is a city and a name test alone
        /// found no owner for it -- an author's lot then showed no way back out of the generated tree. The name test is
        /// kept after it for a root built by hand, where there is no component to ask. </summary>
        public static bool TryFindOwner(Transform t, out Transform owner, out string ownerLabel)
        {
            owner = null;
            ownerLabel = string.Empty;
            Transform walk = t;
            while (walk != null)
            {
                if (walk.GetComponent<RoadIntersection>() != null) { owner = walk; ownerLabel = "intersection"; return true; }
                if (walk.GetComponent<Road>() != null) { owner = walk; ownerLabel = "road"; return true; }
                if (walk.GetComponent<RoadSystem>() != null) { owner = walk; ownerLabel = "road system"; return true; }
                if (walk.GetComponent<CityAuthoring>() != null) { owner = walk; ownerLabel = "city"; return true; }
                if (walk.name == CityHierarchy.RootName) { owner = walk; ownerLabel = "city"; return true; }
                walk = walk.parent;
            }
            return false;
        }
    }
}
#endif
