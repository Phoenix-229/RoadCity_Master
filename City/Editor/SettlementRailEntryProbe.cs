// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-27 / D-6, third bullet. One settlement's answer to one hand-placed railway: whether the author's line
    /// runs THROUGH it, whether a branch would have to be built to reach it, or whether nothing reaches it at all.
    ///
    /// A row rather than a bare report, because the question has two halves and an author needs both. The first half is
    /// the settlement's own <see cref="SettlementRailReport"/> -- does a place of this size and order have a railway at
    /// all -- and the second is <see cref="SettlementRailEntryReport"/>, which is where that railway MEETS it. A row
    /// showing only the second would say "nothing reaches this settlement" for a village the presence curve never gave a
    /// railway to, which is a true sentence about the wrong question.
    ///
    /// The <see cref="refusal"/> field is the third outcome, and it is not either report's: it is the reason the
    /// settlement could not be ASKED -- no source road, so no order; no order, so no reference population; no footprint,
    /// so no boundary to cross. Those are author-fixable scene faults and not facts about a railway, so they are carried
    /// separately rather than folded into a report that would then look like a measurement. </summary>
    public struct SettlementRailEntryRow
    {
        /// <summary> The settlement this row is about. </summary>
        public CityAuthoring city;

        /// <summary> The settlement's stable identity -- its source road and which end of it -- built by the one helper
        /// that builds that phrase, so this row, a siting report and a refusal all name the place the same way. It is
        /// also what the presence draw is keyed on, which is why it is carried and not recomputed. </summary>
        public string identity;

        /// <summary> Which order of place this is and what the catalogue calls it, and the reference population the
        /// presence curve was asked about. Carried because a surprising railway is most often a surprising order: a
        /// hamlet answered as a regional centre gets an intercity station, and the figure that explains it is
        /// here. </summary>
        public int orderIndex;
        public string orderName;
        public int population;

        /// <summary> The settlement's footprint in world XZ, on <c>CityAuthoring.TryRegionWorldXZ</c>'s convention where
        /// the rect's y is world Z. The rect the boundary crossing was computed against. </summary>
        public Rect footprintWorldXZ;

        /// <summary> Whether this place has a railway, from D-6's fitted presence curve. </summary>
        public SettlementRailReport rail;

        /// <summary> Where that railway meets this place, from D-6's third bullet. </summary>
        public SettlementRailEntryReport entry;

        /// <summary> Why the settlement could not be asked at all, empty when it could. </summary>
        public string refusal;

        /// <summary> Whether the two reports mean anything. False means <see cref="refusal"/> says what to fix in the
        /// scene, and neither report was produced. </summary>
        public bool Answered
        {
            get { return string.IsNullOrEmpty(refusal); }
        }

        /// <summary> Whether a branch can be built to this settlement: it has a railway, no main line passes, and the
        /// branch that would reach it is inside the settlement's own fitted track budget. </summary>
        public bool NeedsBranch
        {
            get { return Answered && entry.resolved && entry.approach == SettlementRailApproach.Terminus; }
        }

        /// <summary> One line, in the register's voice: who this is, what order it was answered as, whether it has a
        /// railway, and where that railway meets it. Both reports are quoted through their OWN <c>Describe</c> rather
        /// than re-worded here, so the sentence an inspector shows is the sentence a node's note carries. </summary>
        public string Describe()
        {
            string who = string.IsNullOrEmpty(identity) ? "an unnamed settlement" : identity;
            if (!Answered) { return who + ": " + refusal; }

            string order = string.IsNullOrEmpty(orderName) ? ("order " + orderIndex) : orderName;
            string head = who + " (" + order + ", " + population.ToString("N0", CultureInfo.InvariantCulture)
                          + " people): ";
            if (!rail.resolved || !rail.served) { return head + rail.Describe(); }
            return head + rail.Describe() + " " + entry.Describe();
        }
    }


    /// <summary> B-27 / D-6, third bullet, made reachable. Asks every settlement in the open scene what one hand-placed
    /// railway does for it, and builds the branch where one is owed.
    ///
    /// WHY THIS LIVES ON THE RAILWAY AND NOT IN THE GRAPH. D-6's fit half is a stage-0 node because its inputs are a
    /// fitted table and four figures, all of which a node parameter can carry. This half's input is a PLANNED ALIGNMENT
    /// -- two hundred world-space samples of a line an author dragged out in the scene view -- and the graph's parameters
    /// are Number, Text and Flag only. A node could not be handed the line without a port for it, and nothing publishes
    /// one; inventing a source node that reached into the scene for a component would put scene state inside an operator,
    /// which is exactly what <c>CityBakeRequest</c>'s source nodes exist to keep out. So the author's own railway
    /// inspector is where the question is asked, which is also where both objects are already in front of them.
    ///
    /// WHY THE BRANCH IS A SECOND <c>RailAlignment</c> AND NOT A SECOND BUILDER. A branch to a terminus is a railway. It
    /// has a ruling gradient, a minimum radius, a cant, an earthwork limit and a clearance, and every one of those is
    /// already decided by D-5's planner and cut by D-5's builder. So this file writes an ALIGNMENT and hands it to the
    /// shipped component: the branch is banked, graded, clearance-checked, level-crossing-searched and REFUSED by the
    /// same code as the author's own line, and a fix to either is a fix to both. A second track builder here would be the
    /// shape this register keeps finding broken -- two implementations of one outcome, free to drift.
    ///
    /// WHY THE COPY IS A COMPONENT PASTE. The branch must be made of the same railway as the line it leaves: the same
    /// gauge, the same ballast profile, the same sleeper spacing, the same four materials. <c>RailTrackClass</c>,
    /// <c>RailFormationClass</c> and <c>RailOverheadClass</c> are reference types, so assigning them across would give
    /// the two components ONE class between them and an author editing the branch would silently re-gauge the main line.
    /// Copying thirty-odd fields by hand would be a list that falls behind the next field added. So the whole component's
    /// serialised values are copied through <c>ComponentUtility</c> -- Unity's own deep copy, which follows the
    /// serialiser and therefore carries a field nobody here has heard of -- and only the four things that are properties
    /// of the BRANCH rather than of the line are then overwritten. </summary>
    public static class SettlementRailEntryProbe
    {
        #region "What a branch is called and how it is undone"
        /// <summary> The element-id prefix every branch this file creates carries, so a branch is told apart from a
        /// hand-placed line by the same name parse <c>CityHierarchy.TryParseName</c> already does. </summary>
        public const string BranchElement = "branch.rail.";

        /// <summary> The undo step's name, spelled once so one press collapses to one entry. </summary>
        public const string UndoLabel = "Build the branch to a settlement";
        #endregion


        #region "Asking the scene"
        /// <summary> The settlement's stable identity: its source road and which end of it, through the one helper that
        /// builds that phrase. Never the GameObject's name, which an author is free to rename and which would re-roll
        /// the settlement's railway when they did. </summary>
        public static string IdentityOf(CityAuthoring city)
        {
            if (city == null) { return string.Empty; }
            string roadName = city.sourceRoad != null ? city.sourceRoad.name : string.Empty;
            return SettlementSiting.IdentityOf(roadName, city.sourceEnd);
        }

        /// <summary> What one settlement gets from one planned main line.
        ///
        /// The order is looked up first, because everything after it depends on the order: the presence curve reads the
        /// order's row, and the population it is asked about is that order's own reference place -- the same figure
        /// <c>SettlementSiting</c> proposes a candidate with, so the railway an author sees here is the railway the
        /// siting pass would have given the same settlement.
        ///
        /// The continent is left EMPTY on purpose. A continent is a property of where a settlement sits on a real globe,
        /// which nothing in a scene knows; the fit table answers an empty key from its world row and reports
        /// <c>usedWorldFallback</c>, so the substitution is visible rather than invented. A graph that does know its
        /// continent names it on the rail node, and that answer is the one a bake stamps. </summary>
        public static SettlementRailEntryRow Resolve(CityAuthoring city, SettlementFitTable fit,
            IList<RailAlignmentSample> mainLine, double turnoutRatio)
        {
            SettlementRailEntryRow row = new SettlementRailEntryRow();
            row.city = city;
            row.identity = IdentityOf(city);

            if (city == null)
            {
                row.refusal = "there is no settlement here to ask";
                return row;
            }

            SettlementOrderClass order;
            string orderWhy;
            if (!CityAuthoringEditor.TryOrderOf(city, out order, out orderWhy))
            {
                row.refusal = orderWhy;
                return row;
            }
            row.orderIndex = order.orderIndex;
            row.orderName = order.displayName;
            row.population = order.referencePopulation;

            Rect footprint;
            string footprintWhy;
            if (!city.TryRegionWorldXZ(out footprint, out footprintWhy))
            {
                row.refusal = footprintWhy;
                return row;
            }
            row.footprintWorldXZ = footprint;

            if (fit == null)
            {
                row.refusal = "no settlement fit table could be resolved, so nothing says whether a place of this size"
                              + " has a railway";
                return row;
            }

            row.rail = SettlementRailPresence.Resolve(fit, row.orderIndex, string.Empty, row.population, row.identity,
                (ulong)(uint)city.seed);
            row.entry = SettlementRailEntry.Resolve(footprint, mainLine, row.rail, turnoutRatio);
            return row;
        }

        /// <summary> Every settlement in the open scene, asked the same question against the same line, and how many of
        /// them the line reaches. The fit table is resolved ONCE for the whole sweep -- through the bake's own resolver,
        /// so the inspector and the bake cannot answer from two different assets -- rather than once per settlement,
        /// which would be one project-wide asset search per city.
        ///
        /// Inactive settlements are not asked. <c>FindObjectsByType</c> reads active objects only, and a settlement an
        /// author has switched off is one they have taken out of the scene for now; answering for it would put a station
        /// in a town that is not there. </summary>
        public static int ResolveScene(IList<RailAlignmentSample> mainLine, double turnoutRatio,
            List<SettlementRailEntryRow> rowsOut)
        {
            if (rowsOut == null) { return 0; }
            CityAuthoring[] cities = EngineIntegration.FindObjectsByType<CityAuthoring>();
            if (cities == null || cities.Length == 0) { return 0; }

            SettlementFitTable fit = CityAuthoringEditor.ResolveSettlementFit();
            int reached = 0;
            for (int i = 0; i < cities.Length; i++)
            {
                SettlementRailEntryRow row = Resolve(cities[i], fit, mainLine, turnoutRatio);
                rowsOut.Add(row);
                if (row.Answered && row.entry.resolved && row.entry.approach != SettlementRailApproach.Unserved)
                {
                    reached++;
                }
            }
            return reached;
        }
        #endregion


        #region "Building the branch"
        /// <summary> Build, or rebuild, the branch that reaches one settlement, and say what happened.
        ///
        /// IDEMPOTENT. The branch is found by its hierarchy name before it is created, so pressing the button twice gives
        /// one branch whose alignment was rewritten, not two branches arguing about where the junction is. That is the
        /// same find-before-create rule <c>CityBakeContext.EnsureObject</c> follows, and for the same reason: an author
        /// who moves the main line and presses again is correcting a branch, not adding one.
        ///
        /// FOUR THINGS ARE OVERWRITTEN after the component copy, and each is a property of the branch rather than of the
        /// line it left:
        ///
        ///   1. the alignment, which is the whole point -- junction, turnout lead, terminus;
        ///   2. the track count, forced to one, because a branch built to reach one terminus is single track and a
        ///      double-track formation staked from this centreline would put the second road on a radius nobody checked;
        ///   3. the station stops, cleared, because the main line's stops are distances along the MAIN LINE and carrying
        ///      them over would put a platform at a chainage the branch does not have;
        ///   4. the overhead structures, cleared, for the same reason -- a bridge over the main line is not over this.
        ///
        /// The branch is then PLANNED and built with the shipped pair. A branch the planner refuses is left in the scene
        /// with its refusals showing rather than deleted, because the alignment is correct and the refusal is about the
        /// ground it crosses: an author fixes that by easing a curve or moving the terminus, and they cannot do either to
        /// an object this method threw away. </summary>
        public static string BuildBranch(RailAlignment mainLine, RailAlignmentPlan mainPlan,
            SettlementRailEntryRow row, double turnoutRatio, ITerrainSurfaceReader ground)
        {
            if (mainLine == null) { return "There is no railway to branch from."; }
            if (mainPlan == null || mainPlan.samples.Count < 2)
            {
                return "The main line has not been staked, so there is nowhere for a branch to leave it.";
            }
            if (row.city == null) { return "There is no settlement to build a branch to."; }
            if (!row.Answered) { return "No branch was built: " + row.refusal + "."; }
            if (!row.NeedsBranch) { return NoBranchLine(row); }
            if (mainLine.settings == null || mainLine.settings.track == null || mainLine.settings.formation == null)
            {
                return "The main line has no track class or no formation class assigned, so there is nothing to build "
                       + "the branch to the same standard as.";
            }

            HorizontalAlignment spur = SettlementRailEntry.BuildSpurAlignment(row.entry, mainPlan.samples,
                turnoutRatio, mainLine.settings.formation.trackCentresMetres,
                mainLine.settings.track.designSpeedKmh);
            if (spur == null)
            {
                return "The branch could not be staked: the main line gave no direction at the junction, so there is no"
                       + " turnout lead to leave along.";
            }

            Transform utilities = CityHierarchy.EnsureBranch(row.city.transform, CityBranch.Utilities);
            if (utilities == null)
            {
                return "The settlement has no hierarchy to hang a branch under, so nothing was built.";
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            RailAlignment branch = EnsureBranchComponent(utilities, row, mainLine);
            if (branch == null)
            {
                return "The branch object exists but could not be given a railway component, so nothing was staked.";
            }

            Undo.RecordObject(branch, UndoLabel);
            Undo.RecordObject(branch.transform, UndoLabel);
            branch.settings.plan = spur;
            branch.settings.trackCount = 1;
            if (branch.settings.stationStopMetres != null) { branch.settings.stationStopMetres.Clear(); }
            if (branch.settings.overheadStructures != null) { branch.settings.overheadStructures.Clear(); }
            branch.transform.position = row.entry.junctionPoint;
            EditorUtility.SetDirty(branch);

            string planNote;
            RailAlignmentPlan plan = branch.Plan(ground, null, out planNote);
            if (plan == null || !plan.Buildable)
            {
                Undo.SetCurrentGroupName(UndoLabel);
                Undo.CollapseUndoOperations(undoGroup);
                return RefusedBranchLine(row, plan, planNote);
            }

            string built = RailAlignmentBuilder.Build(branch, plan);
            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
            return BuiltBranchLine(row, plan) + " " + built;
        }

        /// <summary> The branch's own GameObject and component, found before it is created.
        ///
        /// The name is a hierarchy name so the parse that tells a generated object from a hand-placed one works on a
        /// branch too, and the element id carries the settlement's identity with the three characters that parse as
        /// structure replaced -- a road called <c>Main [up]#2</c> is an ordinary thing for an author to type, and
        /// carrying those brackets into the name would produce a name that parses back to the wrong element id.
        ///
        /// The component values are copied from the main line through <c>ComponentUtility</c>, which is Unity's own
        /// serialiser-driven deep copy: it gives the branch its OWN track, formation and overhead class instances rather
        /// than the main line's, and it carries the four material references a JSON round trip would drop. See the class
        /// header for why neither a field-by-field copy nor a plain assignment would do. </summary>
        private static RailAlignment EnsureBranchComponent(Transform utilities, SettlementRailEntryRow row,
            RailAlignment mainLine)
        {
            string wanted = CityHierarchy.NameFor(GeneratedKind.Rail, BranchElement + CleanId(row.identity), 0);
            Transform found = utilities.Find(wanted);

            GameObject host;
            if (found != null)
            {
                host = found.gameObject;
            }
            else
            {
                host = new GameObject(wanted);
                Undo.RegisterCreatedObjectUndo(host, UndoLabel);
                host.transform.SetParent(utilities, false);
            }

            RailAlignment branch = host.GetComponent<RailAlignment>();
            bool fresh = branch == null;
            if (fresh) { branch = Undo.AddComponent<RailAlignment>(host); }
            if (branch == null) { return null; }

            // Only on a FRESH component. Re-pasting on every press would throw away an author's own edits to the
            // branch -- a widened formation, a different ballast material -- every time they corrected the main line,
            // which is the opposite of what pressing the button again means.
            if (fresh)
            {
                ComponentUtility.CopyComponent(mainLine);
                ComponentUtility.PasteComponentValues(branch);
            }
            return branch;
        }

        /// <summary> An element id with the three characters <c>CityHierarchy.TryParseName</c> reads as structure
        /// replaced by dashes, so a generated name always parses back to the element id it was built from. </summary>
        private static string CleanId(string identity)
        {
            if (string.IsNullOrEmpty(identity)) { return "unassigned"; }
            return identity.Replace('[', '-').Replace(']', '-').Replace('#', '-');
        }
        #endregion


        #region "The sentences, pure"
        /// <summary> What the sweep found, as one line. Pure, so D-16's gate can assert the wording with no scene: a
        /// sentence built inside an inspector is a sentence no gate has ever read. </summary>
        public static string SweepLine(int settlementCount, int reachedCount, int branchCount)
        {
            if (settlementCount <= 0)
            {
                return "No settlement stands in this scene, so this line serves none. A railway across open country is "
                       + "a perfectly ordinary thing; settlements are placed from road ends.";
            }
            string places = settlementCount == 1 ? "settlement" : "settlements";
            if (reachedCount <= 0)
            {
                return "This line reaches none of the " + settlementCount + " " + places + " in the scene. Each one "
                       + "below says whether that is because it has no railway at all or because the line passes too "
                       + "far from it.";
            }
            string branches = branchCount == 1 ? "one of them needs a branch" : branchCount + " of them need branches";
            return "This line reaches " + reachedCount + " of the " + settlementCount + " " + places + " in the scene, "
                   + "and " + (branchCount == 0 ? "none of them needs a branch" : branches)
                   + " -- a branch leaves the line at a turnout and ends at a terminus inside the settlement.";
        }

        /// <summary> Why no branch was built, for a row that was asked and answered something other than a terminus.
        /// Three different answers, because an author acts differently on each: a through line needs no branch, an
        /// unserved place may not have one, and a place out of budget is telling them the line is too far away.
        /// </summary>
        public static string NoBranchLine(SettlementRailEntryRow row)
        {
            if (row.entry.approach == SettlementRailApproach.ThroughLine)
            {
                return "No branch is needed: the main line already runs through this settlement, and what it needs is "
                       + "stopping places along the "
                       + row.entry.insideLengthMetres.ToString("0", CultureInfo.InvariantCulture)
                       + " m of it that lie inside.";
            }
            if (!row.rail.resolved || !row.rail.served)
            {
                return "No branch: this settlement has no railway at all, so a line to it would serve nothing. "
                       + row.rail.Describe();
            }
            return "No branch: " + row.entry.Describe();
        }

        /// <summary> What a built branch is, in the figures the branch's own plan produced rather than in the
        /// resolver's straight-line estimate -- the planner lengthens the run as it rounds the turnout, and reporting
        /// the estimate here would be reporting a figure this railway does not have. </summary>
        public static string BuiltBranchLine(SettlementRailEntryRow row, RailAlignmentPlan plan)
        {
            string who = string.IsNullOrEmpty(row.identity) ? "an unnamed settlement" : row.identity;
            string length = plan != null
                ? plan.runLengthMetres.ToString("0", CultureInfo.InvariantCulture)
                : row.entry.spurLengthMetres.ToString("0", CultureInfo.InvariantCulture);
            return "Branch to " + who + ": " + length + " m of single track, leaving the main line "
                   + row.entry.centreToLineMetres.ToString("0", CultureInfo.InvariantCulture)
                   + " m from the settlement's centre and ending at a terminus on the edge of its core.";
        }

        /// <summary> Why a staked branch cannot be laid, and what is left in the scene because of it. The refusals are
        /// the branch component's own and are shown on it; this sentence says where to look rather than repeating
        /// them, because a refusal repeated in two places is a refusal an author fixes in one. </summary>
        public static string RefusedBranchLine(SettlementRailEntryRow row, RailAlignmentPlan plan, string note)
        {
            string who = string.IsNullOrEmpty(row.identity) ? "an unnamed settlement" : row.identity;
            int refusals = plan != null ? plan.refusals.Count : 0;
            if (refusals > 0)
            {
                return "The branch to " + who + " was staked but cannot be laid: " + refusals
                       + (refusals == 1 ? " refusal" : " refusals")
                       + " on the new railway say why, and it is left in the scene under the settlement's Utilities so "
                       + "they can be read and the terminus moved.";
            }
            return "The branch to " + who + " was staked but produced no buildable railway"
                   + (string.IsNullOrEmpty(note) ? "." : " -- " + note + ".")
                   + " It is left in the scene under the settlement's Utilities.";
        }
        #endregion
    }
}
#endif
