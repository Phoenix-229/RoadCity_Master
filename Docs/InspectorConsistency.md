# Inspector consistency checklist — RM-64

This file is RM-64's artefact: the record of which of the tool's fifteen author-facing editor files have been brought
onto the conventions RM-51 established, which will deliberately never be, and which are scheduled with an owner.

**The table below is generated.** It is spliced in from `InspectorConsistencyChecklist` by
`InspectorConsistencyDocWriter`, and `InspectorConsistencyGate` (`RCMCONSIST`) fails the build if this file and that
register disagree. Edit the register, not the table.

## What "converted" means

RM-64's five criteria, applied per file, in its own terms:

1. **Grouping** — fields sit under the four-group vocabulary (`FieldGroup`: what it *is*, what it *looks like*, what it
   *does to the world*, what is *generated from it*), not under ad-hoc headings.
2. **Units** — every numeric field draws its unit, from the field's own descriptor rather than from a typed-in string.
3. **Inline constraints** — every refusal appears at the field, in one style, naming the limit **and its source**. Never
   a silent clamp.
4. **Product name** — every visible product string is the display name, from one constant (C-05).
5. **Foldout persistence** — foldout state is keyed per component **type**, so an author's layout survives a selection
   change.

A file with all five is *converted*. A file with only the fourth has had its header renamed, which is **not** the same
thing — and because the state in the table is derived from the criteria rather than typed in, a row cannot claim
otherwise.

## Two conventions settled once, here

**Where a message goes.** Per-object refusals appear on the object; the aggregate appears in RM-52's status panel. Never
both, never neither. This is `RefusalRouting.Decide`, a total function, so the promise is a property of the code rather
than something to remember while writing the next inspector.

**How a computed value is shown.** Measured lengths, solved radii, instance counts, RM-61's inscribed dimensions: drawn
disabled, under the one group `Measured`, never in an editable field. `MeasuredValuePresentation` holds both the group
label and the list, each entry naming the task that computes it.

## The fifteen files

<!-- checklist-table:begin — generated from InspectorConsistencyChecklist. Do not edit by hand. -->

| File | Author sees | Kind | State | Applied | Still missing | Why |
| --- | --- | --- | --- | --- | --- | --- |
| `EditorMenu.cs` | Menu items | MenuOnly | deliberately unchanged | product name | grouping, units, inline constraints, foldout persistence | Menu paths only. It draws no fields, so four of the five criteria have nothing to attach to; the fifth, the product string, is the one that applies and is applied. Menu path root is the display name. |
| `EditorUtilities.cs` | Shared drawing helpers | SharedHelper | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. The helpers the legacy inspectors draw through. RM-102 routes them at InspectorFieldDrawer, which is where units and inline constraints actually arrive. |
| `HelpWindow.cs` | Help | UtilityWindow | deliberately unchanged | product name | grouping, units, inline constraints, foldout persistence | A text pane with no author-editable numeric field and no constraint to present. Imposing a grouping vocabulary on three paragraphs is churn, which RM-64 names as the thing a consistency pass must not become. Reviewed 2026-08-30. |
| `ObjExporter.cs` | Mesh export command | UtilityWindow | deliberately unchanged | product name | grouping, units, inline constraints, foldout persistence | An export command behind a file dialog, with no editable field set. Its one real defect is RM-63.7's swallowed texture-copy failure, which is a correctness fix rather than a layout one. See RM-63.7. |
| `RoadEditor.cs` | Road | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. The inspector an author lives in. Its fields are already described by RM-51's registry; what is missing is the drawer that reads those descriptors instead of the hand-rolled layout. |
| `RoadIntersectionEditor.cs` | RoadIntersection | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. Also gains RM-97's solved corner radius and sight triangle as MEASURED values, so it must adopt MeasuredValuePresentation rather than draw them as editable floats. |
| `RoadSystemEditor.cs` | RoadSystem | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. Carries RM-52's status panel, so it is where the AGGREGATE half of RefusalRouting lands. Per-object refusals must NOT be repeated here. |
| `SaveWindow.cs` | Group save | UtilityWindow | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. Layout by RM-102, filename determinism by RM-63.6, and the .rao schema it writes by RM-105 -- three owners, one window. |
| `SplineCEditor.cs` | SplineC | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. The alignment component RM-62 makes the head of the chain; RM-95 adds the vertical profile fields it must group. |
| `SplineFEditor.cs` | SplineF | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. |
| `SplineIEditor.cs` | SplineI | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. |
| `SplineNEditor.cs` | SplineN | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. The largest of the family and the one RM-75 names for its node-editing stall; its conversion and that stall are the same read of the same file. |
| `TerrainEditor.cs` | Terrain interaction | ComponentInspector | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. Its fields decide what RM-57's gateway is allowed to write, so its inline constraints are the ones an author most needs to see before a build, not after. |
| `Wizard.cs` | Setup wizard | UtilityWindow | pending | product name | grouping, units, inline constraints, foldout persistence | Owned by RM-102. A stepped dialog: the grouping vocabulary maps onto its steps rather than onto foldouts, which is a documented deviation and not an exemption. |
<!-- checklist-table:end -->

Everything else in `Scripts/Editor/` is § 10's own tooling — the gates, the suite runner, the field model and drawer,
the assembly-boundary audit, RM-65's manual registers and writer, RM-66's disposition register, RM-67's shader and
pipeline inventory with its writer — and is excluded by name rather than by pattern, so a future inspector cannot be
absorbed into the exclusion list by accident. The gate compares the register against the directory: a sixteenth
author-facing editor file with no row fails the build.
