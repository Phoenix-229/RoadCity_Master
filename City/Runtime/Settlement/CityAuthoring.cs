#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-1. One settlement's presence in the scene: the answers only a scene can give, and nothing
    /// else. This is the component the register found missing -- the city model, the operators, the hierarchy and the
    /// bake contract all existed, but nothing in a scene said "a city goes here, from this graph, with this seed".
    ///
    /// It holds SIX answers and one output reference. The test each of the six passes is the same: it names something
    /// that only exists in a scene, so no asset could hold it.
    ///
    ///   1. <see cref="graph"/>             which graph asset to run.
    ///   2. <see cref="seed"/>              which seed to run it with.
    ///   3. <see cref="regionCentre"/> +
    ///      <see cref="regionXZ"/>          where, as a rect around a Transform that can be dragged.
    ///   4. <see cref="prefabLibraries"/>   which content the placement nodes may draw from.
    ///   5. <see cref="archetypeOverride"/> the settlement's character, when the author disagrees with the measurement.
    ///   6. <see cref="scanRoots"/>         which hand-placed objects C-18's sweep should seat.
    ///
    /// And <see cref="cityData"/>, the one output: the asset the bake writes into.
    ///
    /// NO PARAMETER THAT BELONGS TO A NODE LIVES HERE. Not the zone mix, not the prefab-category bindings, not the
    /// per-stage switches. Those are node fields on the graph, and the reason is a failure this register already
    /// records: a component carrying them would have to be kept in step with the graph by hand, which is exactly how
    /// <c>Road.surfaceType</c> became a serialised field with zero draw sites -- a field and its row are two edits and
    /// only one of them happened. Derived state lives in <see cref="CityData"/> and in the branch GameObjects, as it
    /// does today.
    ///
    /// The settlement's ORDER -- capital, city, town, village -- is deliberately absent. It comes from the
    /// <see cref="SettlementRoleEnum"/> on the road at the terminus this settlement was sited from, because the roads
    /// determine the settlements: the author draws and names the network, and a road's free end is where a place
    /// begins. An order field here would be a second place to say it, free to disagree with the road.
    /// <see cref="TryGetSourceRole"/> is how the order is reached; the role-to-order table itself belongs to C-15's
    /// <c>SettlementOrderCatalog</c>, not to this file.
    ///
    /// <see cref="regionXZ"/> is PROPOSED, not blank. C-14 sizes it from the terminus and the order's fitted area, and
    /// an author who drags it is overriding a derivation. That is why a zeroed region is reported as an author having
    /// cleared it rather than as a field never filled in.
    ///
    /// Placement in the Hierarchy: a SIBLING of the roads, under the same <c>RoadSystem</c> parent -- "the city which
    /// is the child of the parent RoadCityMaster". Its own five generated branches hang beneath it through
    /// <see cref="CityHierarchy.EnsureBranch"/>.
    ///
    /// This file computes and refuses; it never mutates a scene, never touches an asset, and never draws. C-3 creates
    /// these components, C-6's runner reads them, C-9's inspector draws them, and C-9 owns the Scene-view handle for
    /// the region -- one owner each, so the rect is not drawn twice by two files free to disagree. </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CityAuthoring : MonoBehaviour
    {
        // ------------------------------------------------------------------ the six scene-side answers

        /// <summary> 1. The graph to run. Nothing happens without one, and C-3 assigns a graph pre-wired to the
        /// default chain when it creates the city, so a newly added settlement is runnable rather than a component
        /// pointing at nothing. An author who wants a blank canvas deletes nodes. </summary>
        public CityGraph graph;

        /// <summary> 2. The seed. Everything derived is a deterministic function of this plus the graph, so the same
        /// pair reproduces the same settlement byte for byte -- <c>CitySeed.ForOperator</c> and <c>CitySeed.ForItem</c>
        /// derive every per-stage and per-item stream from it, and no operator may reach for <c>System.Random</c> or
        /// the clock.
        ///
        /// It defaults to 0 rather than to a random number, because a random default is a scene that changes when it
        /// is opened. Two settlements sharing a seed and a graph would come out identical, which is the opposite of
        /// what this feature is for, so C-3 assigns a distinct seed per city as it creates it. </summary>
        public int seed;

        /// <summary> 3a. The Transform the region is measured from, so the whole settlement can be dragged in the
        /// Scene view without editing four numbers. Null means this component's own Transform, which is the common
        /// case: the city GameObject IS the centre. Naming a separate Transform is for the case where the centre
        /// should follow something else -- a terminus marker, a harbour mouth. </summary>
        public Transform regionCentre;

        /// <summary> 3b. The region, in metres, RELATIVE to <see cref="regionCentre"/> and in the XZ plane: <c>x</c>
        /// and <c>width</c> run along world X, <c>y</c> and <c>height</c> along world Z. Y is not stored -- ground
        /// height is sampled, never authored, which is the whole tool's convention.
        ///
        /// The default is a 500 m square centred on the anchor, which is a town-sized footprint and deliberately not
        /// zero: an empty rect would read as "not set yet" when in fact C-14 proposes this value. Use
        /// <see cref="TryRegionWorldXZ"/> rather than this field directly -- it resolves the centre and refuses a
        /// region that encloses no ground. </summary>
        public Rect regionXZ = new Rect(-250f, -250f, 500f, 500f);

        /// <summary> 4. The content the placement nodes may draw from, in priority order: the first library that has a
        /// wired, validated entry for a category wins, so a regional library can be laid over a base one without
        /// editing either. An empty list is not an error -- a graph that only zones and terraforms places no prefabs,
        /// and content is refused per category by <c>ContentResolver</c> with a named reason, not wholesale here. Read
        /// it through <see cref="ResolvedLibraries"/>, which drops the empty slots an author leaves behind. </summary>
        public List<CategoryContentSet> prefabLibraries = new List<CategoryContentSet>();

        /// <summary> 5. The settlement's character, and an OVERRIDE rather than a setting. It defaults to
        /// <c>SettlementArchetype.Auto</c>, which means "measure it": the siting pass reads the water adjacency it
        /// already has to compute and decides port, beach, lakeside or plain from the ground itself. An author sets
        /// this only to disagree with that measurement, and the inspector shows which archetype was measured beside
        /// whichever one is in force, so an override is visible as an override.
        ///
        /// The archetype earns its place on the component because it is a claim about THIS scene's water. Which
        /// <c>ZoneType</c>s each archetype admits -- the quay zone for a port, the setback band for a beach -- is
        /// C-15's table, not a field here. </summary>
        public SettlementArchetype archetypeOverride = SettlementArchetype.Auto;

        /// <summary> 6. The roots C-18's sweep walks to seat the author's own hand-placed objects: every Transform
        /// beneath one of these that has renderer or collider bounds is scanned, whether or not it carries a component
        /// of this tool, because the author's buildings will be plain prefabs.
        ///
        /// Empty by default, and that is a real default rather than a stub: an author who has placed nothing has
        /// nothing to seat, and a sweep that defaulted to the whole scene would flatten ground under objects nobody
        /// asked about. Read it through <see cref="ResolvedScanRoots"/>. </summary>
        public List<Transform> scanRoots = new List<Transform>();

        // ------------------------------------------------------------------ the one output

        /// <summary> The asset the bake writes into, and the only output reference here. It is a reference and not
        /// embedded state so that a baked settlement opens and enters play mode with no generator running: the scene
        /// holds the branch GameObjects, the asset holds the network, blocks, lots and buildings that produced them.
        /// </summary>
        public CityData cityData;

        // ------------------------------------------------------------------ provenance: written by C-3, read by C-9

        /// <summary> The road this settlement was sited from. NOT an author's answer -- C-3's <i>Build settlements from
        /// roads</i> writes it as it creates the component, and the inspector shows it read-only.
        ///
        /// It exists because the order has to be reachable. "The order comes from the settlement role at the terminus
        /// this settlement was sited from" is only implementable if something records which terminus that was; without
        /// this reference the rule would be a sentence with nothing behind it. Keeping it as a link rather than as a
        /// copied order value is the point: the author changes the road's role, and the settlement's order follows,
        /// because there is still only one place the answer lives. </summary>
        public Road sourceRoad;

        /// <summary> Which end of <see cref="sourceRoad"/> this settlement stands at, so two settlements on one road --
        /// a village at each end of a village connector -- stay distinguishable. </summary>
        public RoadEnd sourceEnd = RoadEnd.Start;

        // ------------------------------------------------------------------ pure readers

        /// <summary> The world position the region is measured from: <see cref="regionCentre"/> if one is named, this
        /// component's own Transform otherwise. </summary>
        public Vector3 CentreWorld()
        {
            return regionCentre != null ? regionCentre.position : transform.position;
        }

        /// <summary> The region in WORLD XZ metres, or a refusal that names the number that is wrong. Callers use this
        /// instead of reading <see cref="regionXZ"/>, so the relative-to-a-Transform convention is applied in exactly
        /// one place. </summary>
        public bool TryRegionWorldXZ(out Rect worldXZ, out string reason)
        {
            worldXZ = new Rect();
            if (regionXZ.width <= 0f || regionXZ.height <= 0f)
            {
                reason = "the region measures " + regionXZ.width.ToString("0.##") + " m by "
                         + regionXZ.height.ToString("0.##") + " m, so it encloses no ground. C-14 proposes a region"
                         + " from the terminus and the order's fitted area, which means a zero or negative one is an"
                         + " author having cleared a derived value rather than a field nobody filled in";
                return false;
            }
            Vector3 centre = CentreWorld();
            worldXZ = new Rect(regionXZ.x + centre.x, regionXZ.y + centre.z, regionXZ.width, regionXZ.height);
            reason = string.Empty;
            return true;
        }

        /// <summary> The settlement-network role of the road this settlement stands on, which is what its order is
        /// derived from. Refuses, with the reason, when there is no road to read or the road no longer declares a role
        /// -- both of which leave a settlement standing there with nothing to say what it should be. The role-to-order
        /// mapping is C-15's; this only fetches the role. </summary>
        public bool TryGetSourceRole(out SettlementRoleEnum role, out string reason)
        {
            role = SettlementRoleEnum.None;
            if (sourceRoad == null)
            {
                reason = "this settlement records no road it was sited from, so there is no settlement role to read and"
                         + " no order to derive from it. Either it was added by hand rather than by Build settlements"
                         + " from roads, or the road it came from has since been deleted";
                return false;
            }
            role = sourceRoad.settlementRole;
            if (role == SettlementRoleEnum.None)
            {
                reason = "the road this settlement was sited from -- " + sourceRoad.name + " -- no longer declares a"
                         + " settlement role, so it seeds nothing. The settlement is orphaned rather than wrong: set"
                         + " the role again, or delete the settlement";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> The wired libraries, in author order, with the empty slots and the duplicates removed. An author
        /// who pressed + twice would otherwise hold one library twice, and first-match-wins would then be answered by
        /// the same asset in two passes. </summary>
        public List<CategoryContentSet> ResolvedLibraries()
        {
            List<CategoryContentSet> resolved = new List<CategoryContentSet>();
            if (prefabLibraries == null) { return resolved; }
            for (int i = 0; i < prefabLibraries.Count; i++)
            {
                CategoryContentSet set = prefabLibraries[i];
                if (set == null) { continue; }
                if (!resolved.Contains(set)) { resolved.Add(set); }
            }
            return resolved;
        }

        /// <summary> The scan roots, with the empty slots and the duplicates removed, so C-18's sweep cannot visit one
        /// subtree twice and write two pad requests for the same building. </summary>
        public List<Transform> ResolvedScanRoots()
        {
            List<Transform> resolved = new List<Transform>();
            if (scanRoots == null) { return resolved; }
            for (int i = 0; i < scanRoots.Count; i++)
            {
                Transform root = scanRoots[i];
                if (root == null) { continue; }
                if (!resolved.Contains(root)) { resolved.Add(root); }
            }
            return resolved;
        }

        /// <summary> Whether the SCENE-SIDE answers are complete enough for a bake to start, and if not, which one is
        /// missing. Deliberately narrow: it checks the answers this component owns and nothing else. Whether the graph
        /// itself is sound is <c>CityGraph.Validate</c>'s question, and whether a category has content is
        /// <c>ContentResolver</c>'s -- duplicating either here would be a second verdict on the same thing, free to
        /// disagree with the first. </summary>
        public bool ReadyToBake(out string reason)
        {
            if (graph == null)
            {
                reason = "no city graph is assigned, so there is nothing to run. C-3 assigns one when it creates a city;"
                         + " a settlement without one was either hand-added or has had its graph deleted";
                return false;
            }
            Rect world;
            string why;
            if (!TryRegionWorldXZ(out world, out why)) { reason = why; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
