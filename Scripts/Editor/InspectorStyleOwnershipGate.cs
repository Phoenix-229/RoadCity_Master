// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-109's gate for B-3 and B-4: proves that this tool sets label wrapping and label-column width for
    /// its own drawing pass and gives both back, without opening a single inspector.
    ///
    /// Why a gate and not just the fix. The leak this closes was invisible from inside the tool -- the damage showed up
    /// in OTHER windows, hours later, and looked like Unity misbehaving. A fix whose absence nothing detects comes back:
    /// the next author who wants a wrapped paragraph writes the one line that reads as obvious, `EditorStyles.label
    /// .wordWrap = true`, and there is no compiler error and no visible symptom in the inspector they are working on.
    /// Case 1 is what makes that line fail the build instead.
    ///
    /// Six cases. Nothing outside the owning class writes to a shared style, the active skin or the label width; each of
    /// the six drawing entry points asks for the layout; each asks from the PER-REPAINT path rather than from a one-shot
    /// `Init()`, which is the mistake the original made four times over; the restore is armed with a named method, so
    /// the `-=` can actually remove it; the round trip really restores, checked by doing it; and the yardstick the column
    /// is measured from is still the widest prefix label the tool draws. Report lines are prefixed RCMSTYLE.
    ///
    /// Case 1 exempts the owning file, because writing those settings is its job -- with one exception, which case 5
    /// carries: the ACTIVE SKIN is barred to the owner as well. `GUI.skin` cannot be read outside a GUI callback and the
    /// owner's restore runs on `delayCall`, which is not one; it is also per-pass state that can differ between windows,
    /// so a value captured in one pass and put back in another lands on the wrong skin. The first version of both files
    /// did that and threw `ArgumentException: You can only call GUI functions from inside OnGUI`. So nothing here reads
    /// `GUI.skin` either, and case 5's last assertion is what keeps it that way.
    ///
    /// What it does NOT prove: that the result looks right. Whether `Gizmo new Node preview Color:` now fits on one line
    /// on this author's display, at their font scaling, in a docked inspector of their width, is theirs to see. </summary>
    public static class InspectorStyleOwnershipGate
    {
        #region "Entry points"
        [MenuItem("Window/Road City Master/Testing/Inspector style ownership (B-3, B-4)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }


        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.InspectorStyleOwnershipGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }


        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSTYLE BEGIN\n");
            List<string> failures = new List<string>();

            CaseNothingElseWritesSharedState(builder, failures);
            CaseEveryDrawingFileAsksForIt(builder, failures);
            CaseAskedFromThePerRepaintPath(builder, failures);
            CaseRestoreIsANamedMethod(builder, failures);
            CaseRoundTripRestores(builder, failures);
            CaseYardstickIsStillTheWidest(builder, failures);

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMSTYLE FAIL   ").Append(failures[i]).Append("\n");
            }
            builder.Append("RCMSTYLE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSTYLE END");

            report = builder.ToString();
            return failures.Count == 0;
        }
        #endregion


        #region "Shared parts"
        /// <summary> The owning class: the one file allowed to write to the shared settings. </summary>
        private const string Owner = "InspectorTextLayout.cs";

        /// <summary> The drawing entry points that ask for the layout. Named rather than discovered, because the point
        /// of the case is that these SPECIFIC files -- the ones that used to write the shared styles themselves -- still
        /// get their wrapping through the owner. A new inspector that never wrapped anything needs no entry here.
        /// </summary>
        private static readonly string[] Callers =
        {
            "RoadEditor.cs",
            "RoadSystemEditor.cs",
            "RoadIntersectionEditor.cs",
            "SplineNEditor.cs",
            "HelpWindow.cs",
            "Wizard.cs",
        };

        /// <summary> The methods Unity calls once per repaint. `DoGUI` is the wizard's -- its `OnGUI` does nothing but
        /// call it -- and is listed because the layout has to be asked for where the drawing happens. </summary>
        private static readonly string[] PerRepaintMethods = { "OnInspectorGUI", "OnGUI", "DoGUI" };

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }


        /// <summary> Every `.cs` in the tool, both assemblies, so a City-side file cannot leak what the road side has
        /// stopped leaking. </summary>
        private static string[] AllSources()
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster");
            if (!Directory.Exists(root)) { return new string[0]; }
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        }


        /// <summary> A source line with its `//` comment removed, so a case cannot be failed by a line that merely
        /// QUOTES the forbidden assignment -- the register's own entries do, and so do the comments at the six call
        /// sites explaining what they replaced. String literals are not stripped: no line in this tool puts one of these
        /// assignments inside a string, and pretending to parse C# properly here would be a worse lie than this. </summary>
        private static string WithoutComment(string line)
        {
            int at = line.IndexOf("//");
            return at < 0 ? line : line.Substring(0, at);
        }
        #endregion


        #region "Case 1 -- nothing else writes the shared state"
        /// <summary> An assignment INTO a style the editor owns, or into the global label width. `new GUIStyle(...)` is
        /// excluded because copying a style and configuring the copy is the correct thing and reads almost the same.
        /// </summary>
        private static readonly Regex SharedWrite = new Regex(
            @"(EditorStyles\s*\.\s*\w+\s*\.\s*\w+\s*=(?!=)"
            + @"|GUI\s*\.\s*skin\s*\.\s*\w+\s*\.\s*\w+\s*=(?!=)"
            + @"|EditorGUIUtility\s*\.\s*labelWidth\s*=(?!=))");

        /// <summary> Case 1. The whole point of B-3. One file writes the shared settings; every other file in the tool
        /// asks that file. This is the case that turns the obvious-looking one-liner into a build failure. </summary>
        private static void CaseNothingElseWritesSharedState(StringBuilder builder, List<string> failures)
        {
            string[] sources = AllSources();
            if (sources.Length == 0)
            {
                failures.Add("case1: no sources found under Assets/RoadCityMaster, so nothing could be checked");
                return;
            }

            int offenders = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                string name = Path.GetFileName(sources[i]);
                if (name == Owner) { continue; }
                if (name == "InspectorStyleOwnershipGate.cs") { continue; }   // this file names the pattern to find it

                string[] lines = File.ReadAllLines(sources[i]);
                for (int l = 0; l < lines.Length; l++)
                {
                    string code = WithoutComment(lines[l]);
                    if (code.Contains("new GUIStyle")) { continue; }
                    if (!SharedWrite.IsMatch(code)) { continue; }

                    offenders++;
                    failures.Add("case1: " + name + ":" + (l + 1).ToString()
                        + " writes a setting the editor shares -- '" + code.Trim()
                        + "'. Call InspectorTextLayout.Apply() from the per-repaint path instead; it gives the value"
                        + " back on the next tick, which a raw assignment never does.");
                }
            }

            builder.Append("RCMSTYLE case1  sources ").Append(sources.Length)
                   .Append("  offending writes ").Append(offenders).Append("\n");
        }
        #endregion


        #region "Cases 2 and 3 -- who asks, and from where"
        /// <summary> Case 2. The six files that used to wrap the shared styles themselves still get wrapping -- through
        /// the owner. Without this, "no file writes EditorStyles" would be satisfiable by deleting the wrap entirely and
        /// clipping every help paragraph in the tool. </summary>
        private static void CaseEveryDrawingFileAsksForIt(StringBuilder builder, List<string> failures)
        {
            int asking = 0;
            for (int i = 0; i < Callers.Length; i++)
            {
                string path = Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), Callers[i]);
                if (!File.Exists(path))
                {
                    failures.Add("case2: '" + Callers[i] + "' is not on disk, so its wrapping could not be checked");
                    continue;
                }

                string text = File.ReadAllText(path);
                if (text.Contains("InspectorTextLayout.Apply()")) { asking++; }
                else
                {
                    failures.Add("case2: " + Callers[i] + " never calls InspectorTextLayout.Apply(), so its long"
                        + " explanatory labels no longer wrap -- they clip instead, with no compiler complaint.");
                }
            }

            builder.Append("RCMSTYLE case2  drawing files ").Append(Callers.Length)
                   .Append("  asking ").Append(asking).Append("\n");
        }


        /// <summary> The declaration of the method a line sits in: walk UP from the call to the first line that looks
        /// like a method signature. Crude on purpose -- it is looking for one identifier, in files it already knows the
        /// shape of, and a real parser here would be more code than the thing it guards. </summary>
        private static string EnclosingMethod(string[] lines, int callLine)
        {
            Regex signature = new Regex(@"^\s*(?:\[[^\]]*\]\s*)?(?:public|private|protected|internal|override|static"
                + @"|virtual|sealed|\s)*\s(?:void|bool|int|float|string)\s+(\w+)\s*\(");
            for (int l = callLine; l >= 0; l--)
            {
                Match m = signature.Match(lines[l]);
                if (m.Success) { return m.Groups[1].Value; }
            }
            return "<none>";
        }


        /// <summary> Case 3. The mistake the original made four times over, and the one a reviewer would not see. Asking
        /// from `Init()` -- which runs once behind an `isInitialized` flag -- is enough to leak the styles permanently,
        /// and, now that they are given back on the next tick, NOT enough to keep this tool's own paragraphs wrapped
        /// past the first repaint. Both faults, from the same one-line placement. </summary>
        private static void CaseAskedFromThePerRepaintPath(StringBuilder builder, List<string> failures)
        {
            int checkedCalls = 0;
            for (int i = 0; i < Callers.Length; i++)
            {
                string path = Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), Callers[i]);
                if (!File.Exists(path)) { continue; }

                string[] lines = File.ReadAllLines(path);
                for (int l = 0; l < lines.Length; l++)
                {
                    if (!WithoutComment(lines[l]).Contains("InspectorTextLayout.Apply()")) { continue; }
                    checkedCalls++;

                    string method = EnclosingMethod(lines, l);
                    bool perRepaint = false;
                    for (int m = 0; m < PerRepaintMethods.Length; m++)
                    {
                        if (method == PerRepaintMethods[m]) { perRepaint = true; break; }
                    }

                    Expect(perRepaint, "case3: " + Callers[i] + ":" + (l + 1).ToString()
                        + " asks for the layout from '" + method + "', which is not a per-repaint method. From a"
                        + " one-shot method the wrap is set once: enough to leak it editor-wide, not enough to keep"
                        + " this tool's own paragraphs wrapped after the first repaint.", failures);
                }
            }

            builder.Append("RCMSTYLE case3  calls checked ").Append(checkedCalls).Append("\n");
        }
        #endregion


        #region "Case 4 -- the restore is armed with something removable"
        /// <summary> Case 4. `EditorApplication.delayCall` is a plain multicast delegate FIELD, not an event with add
        /// and remove accessors, and a lambda subscribed to it with `+=` cannot be taken off again -- a later `-=` on a
        /// fresh closure removes nothing at all. Armed that way the restore still runs, but the subscription list grows
        /// for the life of the domain. So the owner is read here and held to the named form, together with the paired
        /// `-=` that keeps it to one subscription. </summary>
        private static void CaseRestoreIsANamedMethod(StringBuilder builder, List<string> failures)
        {
            string path = Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), Owner);
            if (!File.Exists(path))
            {
                failures.Add("case4: the owner '" + Owner + "' is not on disk, so the restore could not be checked");
                builder.Append("RCMSTYLE case4  owner missing\n");
                return;
            }

            string[] lines = File.ReadAllLines(path);
            bool unsubscribes = false;
            bool subscribes = false;
            int anonymous = 0;
            for (int l = 0; l < lines.Length; l++)
            {
                string code = WithoutComment(lines[l]);
                if (!code.Contains("delayCall")) { continue; }
                if (code.Contains("-= RestoreNow")) { unsubscribes = true; }
                if (code.Contains("+= RestoreNow")) { subscribes = true; }
                if (code.Contains("+= delegate") || code.Contains("=>")) { anonymous++; }
            }

            Expect(subscribes, "case4: " + Owner + " never arms the restore with '+= RestoreNow', so nothing hands the"
                + " shared settings back and the leak this closes is wide open again.", failures);
            Expect(unsubscribes, "case4: " + Owner + " arms the restore without the paired '-= RestoreNow', so a"
                + " repaint that lands before the pending call fires subscribes a second time.", failures);
            Expect(anonymous == 0, "case4: " + Owner + " subscribes an anonymous delegate to delayCall. It is a plain"
                + " multicast field, so a later '-=' on a new closure removes nothing and the list grows every"
                + " repaint. Use the named RestoreNow.", failures);

            builder.Append("RCMSTYLE case4  named arm ").Append(subscribes ? "yes" : "no")
                   .Append("  paired unsubscribe ").Append(unsubscribes ? "yes" : "no")
                   .Append("  anonymous ").Append(anonymous).Append("\n");
        }
        #endregion


        #region "Case 5 -- the round trip really restores"
        /// <summary> Case 5. The only case here that RUNS the thing rather than reading it. Three values are noted, the
        /// layout is applied, the restore is called, and the three have to come back exactly. Every other case is a
        /// source check and would pass just as happily against a restore that put back the wrong field.
        ///
        /// A pending restore is drained FIRST. Otherwise, if a real inspector repainted a moment ago, the values noted
        /// below would be the TOOL's -- and the restore, which puts back what the editor had before this tool ever
        /// wrote to it, would correctly disagree with them and so fail a working fix.
        ///
        /// **This case may not read `GUI.skin`, and neither may the owner.** This gate runs from a menu item and from
        /// batch mode, so it is not inside a GUI callback, and `GUI.skin` outside one throws
        /// `ArgumentException: You can only call GUI functions from inside OnGUI`. The first version of this case read
        /// it three times and the owner's restore read it once, which is where that exception came from. `GUI.skin` is
        /// also per-pass state that can differ between windows, so a value captured in one pass and put back in another
        /// would be written onto the wrong skin. The FIRST assertion below is what keeps it out: a source check that the
        /// owner never mentions `GUI.skin` again. A `GUILayout.Label` that needs wrapping passes
        /// `EditorStyles.wordWrappedLabel` instead.
        ///
        /// Only the ROUND TRIP is skipped when there is no skin, and the source assertion runs ahead of the skip rather
        /// than after it, which is the whole reason for that order: reading a file needs no skin, so the half of this
        /// case that can run in batch mode does.
        ///
        /// `EditorStyles.label == null` was the test that stood where the skip is, and it never once ran. That property
        /// returns a member of a static the editor fills in when it builds the skin and its getter does not check that
        /// static first, so in `-batchmode -nographics` the read throws a NullReferenceException and the comparison
        /// meant to prevent it never gets to evaluate. The throw came out of the guard line itself -- this gate reported
        /// `threw 1` from the one line written to stop exactly that. The test is now
        /// <see cref="InspectorTextLayout.SkinIsAvailable"/>, which probes instead of comparing. </summary>
        private static void CaseRoundTripRestores(StringBuilder builder, List<string> failures)
        {
            // The source half runs FIRST because it is a file read and needs no skin, so it is the half that still
            // proves something in batch mode. It is also what keeps the round trip below legal in the first place:
            // whatever Apply sets, RestoreNow has to be able to put back off-pass, and GUI.skin is the member that
            // cannot be.
            string ownerPath = Path.Combine(InspectorConsistencyDocWriter.EditorScriptFolder(), Owner);
            int skinReads = 0;
            if (File.Exists(ownerPath))
            {
                string[] ownerLines = File.ReadAllLines(ownerPath);
                for (int l = 0; l < ownerLines.Length; l++)
                {
                    if (WithoutComment(ownerLines[l]).Contains("GUI.skin")) { skinReads++; }
                }
            }
            Expect(skinReads == 0, "case5: " + Owner + " reads GUI.skin on " + skinReads.ToString() + " line(s). The"
                + " restore runs on delayCall, which is not a GUI callback, so that throws 'You can only call GUI"
                + " functions from inside OnGUI' -- and GUI.skin is per-pass state, so a value captured in one pass"
                + " would be put back onto whichever skin is current later. Pass EditorStyles.wordWrappedLabel to the"
                + " GUILayout.Label that needs wrapping instead.", failures);

            if (!InspectorTextLayout.SkinIsAvailable())
            {
                builder.Append("RCMSTYLE case5  owner GUI.skin reads ").Append(skinReads.ToString())
                       .Append("  round trip skipped, this process has no editor skin\n");
                return;
            }

            InspectorTextLayout.RestoreNow();   // drain anything a real repaint left armed

            bool labelWas = EditorStyles.label.wordWrap;
            bool miniWas = EditorStyles.miniLabel.wordWrap;
            float widthWas = EditorGUIUtility.labelWidth;

            InspectorTextLayout.Apply();

            Expect(EditorStyles.label.wordWrap, "case5: Apply() left EditorStyles.label unwrapped, so this tool's own"
                + " help paragraphs clip instead of wrapping.", failures);
            Expect(EditorStyles.miniLabel.wordWrap, "case5: Apply() left EditorStyles.miniLabel unwrapped.", failures);
            Expect(EditorGUIUtility.labelWidth >= widthWas, "case5: Apply() made the label column NARROWER ("
                + widthWas.ToString("F0") + " to " + EditorGUIUtility.labelWidth.ToString("F0") + "px). It may only"
                + " ever widen: on a narrow inspector Unity's own choice is the better one.", failures);

            InspectorTextLayout.RestoreNow();

            Expect(EditorStyles.label.wordWrap == labelWas, "case5: EditorStyles.label.wordWrap came back "
                + EditorStyles.label.wordWrap.ToString() + " instead of the editor's own " + labelWas.ToString()
                + ", so every other window inherits this tool's setting until the next domain reload.", failures);
            Expect(EditorStyles.miniLabel.wordWrap == miniWas, "case5: EditorStyles.miniLabel.wordWrap was not put"
                + " back (" + EditorStyles.miniLabel.wordWrap.ToString() + " instead of " + miniWas.ToString() + ").",
                failures);
            Expect(Mathf.Abs(EditorGUIUtility.labelWidth - widthWas) < 0.01f, "case5: the label column was not put"
                + " back (" + EditorGUIUtility.labelWidth.ToString("F1") + "px instead of "
                + widthWas.ToString("F1") + "px), so every inspector keeps this tool's column width.", failures);

            builder.Append("RCMSTYLE case5  owner GUI.skin reads ").Append(skinReads.ToString())
                   .Append("  editor's own wraps ").Append(labelWas ? "T" : "F")
                   .Append(miniWas ? "T" : "F")
                   .Append("  column ").Append(widthWas.ToString("F0")).Append("px  round trip run\n");
        }
        #endregion


        #region "Case 6 -- the yardstick has not gone stale"
        /// <summary> A labelled field call whose prefix label is a literal: the control forms, where the first argument
        /// is the label. A one-argument `LabelField("...")` is deliberately NOT here -- that draws a full-width label,
        /// not a prefix, and counting the tool's 400-character help paragraphs as prefix labels is the mistake this
        /// sweep made the first time it was run by hand. </summary>
        private static readonly Regex PrefixLabelCall = new Regex(
            @"EditorGUILayout\s*\.\s*(?:Toggle|ToggleLeft|Slider|IntSlider|FloatField|IntField|DoubleField|LongField"
            + @"|TextField|DelayedFloatField|DelayedIntField|DelayedTextField|ColorField|ObjectField|CurveField"
            + @"|Vector2Field|Vector3Field|Vector4Field|EnumPopup|Popup|IntPopup|LayerField|TagField|MaskField)"
            + @"\s*\(\s*""([^""]*)""\s*,");

        /// <summary> The other way this tool now writes a prefix label: inside a `GUIContent`, because B-5 gave every
        /// control a tooltip and a tooltip has nowhere else to go. `new GUIContent("Lane width", "...")` reaches the same
        /// label column as `FloatField("Lane width", ...)` did, and the regex above cannot see it -- so without this the
        /// yardstick sweep would have gone quietly blind to the very files B-5 rewrote, which is how a sized column
        /// becomes a narrow one again.
        ///
        /// Only the FIRST argument is captured, and only when it is a literal. The second is the tooltip: a sentence,
        /// never a prefix label, and comparing it against the yardstick would fail on every well-described field in the
        /// tool. A `GUIContent` built from a variable is invisible here for the same reason it is invisible above -- a
        /// sweep over source text can only measure the strings that are IN the source text. </summary>
        private static readonly Regex GuiContentLabel = new Regex(
            @"new\s+GUIContent\s*\(\s*""([^""]*)""");

        /// <summary> Case 6. The column is measured from ONE string, <see cref="InspectorTextLayout.WidestLabel"/>. Add
        /// a longer prefix label anywhere in the tool and that measurement quietly becomes too narrow again -- which is
        /// the fault B-4 was, arrived at from the other direction. So the sweep that found the yardstick is re-run here,
        /// over all three ways this tool draws a prefix label: the legacy `EditorGUILayout` calls with a literal, the
        /// `GUIContent` form B-5 moved the tooltipped controls onto, and RM-51's registry, whose labels come from the
        /// descriptor rather than from any literal in the file.
        ///
        /// Compared by character count, not pixels. A 33-character label of narrow glyphs can be narrower than a
        /// 32-character one, so this can in principle flag a label that would have fitted; the cost of that is one
        /// changed const, and the alternative -- measuring -- needs an editor skin a batch run has no reason to have.
        /// Length is the signal that survives running headless. </summary>
        private static void CaseYardstickIsStillTheWidest(StringBuilder builder, List<string> failures)
        {
            string longest = InspectorTextLayout.WidestLabel;
            string where = "the yardstick itself";
            string[] sources = AllSources();

            for (int i = 0; i < sources.Length; i++)
            {
                string[] lines = File.ReadAllLines(sources[i]);
                for (int l = 0; l < lines.Length; l++)
                {
                    string code = WithoutComment(lines[l]);
                    Widest(PrefixLabelCall.Matches(code), sources[i], l, ref longest, ref where);
                    Widest(GuiContentLabel.Matches(code), sources[i], l, ref longest, ref where);
                }
            }

            // RM-51's fields are labelled from the registry rather than from a literal at the call site, so the string
            // that reaches the column is the descriptor's own label. It used to be that label with the unit appended,
            // composed here to match the drawer exactly; B-5 fault 5 moved the unit into a column of its own, so the
            // label is now simply the label. The coupling this comment recorded still holds in the other direction: if
            // the drawer ever puts anything back into the prefix label, this has to compose it too.
            IList<FieldDescriptor> fields = InspectorFieldRegistry.All();
            for (int i = 0; i < fields.Count; i++)
            {
                string label = fields[i].label;
                if (label.Length <= longest.Length) { continue; }
                longest = label;
                where = "the RM-51 registry, " + fields[i].component + "." + fields[i].field;
            }

            Expect(longest.Length <= InspectorTextLayout.WidestLabel.Length, "case6: '" + longest + "' ("
                + longest.Length.ToString() + " chars, at " + where + ") is a longer prefix label than the yardstick"
                + " InspectorTextLayout.WidestLabel ('" + InspectorTextLayout.WidestLabel + "', "
                + InspectorTextLayout.WidestLabel.Length.ToString() + " chars). The column is measured from the"
                + " yardstick, so it is now too narrow for this label and the label overflows the row it was given --"
                + " B-4 over again. Set WidestLabel to the longer string.", failures);

            builder.Append("RCMSTYLE case6  yardstick ").Append(InspectorTextLayout.WidestLabel.Length)
                   .Append(" chars  widest found ").Append(longest.Length)
                   .Append("  registry labels ").Append(fields.Count)
                   .Append("  sources swept ").Append(sources.Length).Append("\n");
        }


        /// <summary> Keep the longest label found so far, over one line's matches from one of the two label patterns.
        ///
        /// Its own method because there are two patterns now and the alternative was the same four lines written twice --
        /// which, on a sweep whose whole purpose is to notice a label the tool stopped measuring, is exactly the place a
        /// divergence would go unseen. Both patterns capture the label in group 1, which is what lets one body serve
        /// them. </summary>
        private static void Widest(MatchCollection found, string source, int line, ref string longest, ref string where)
        {
            for (int m = 0; m < found.Count; m++)
            {
                string label = found[m].Groups[1].Value;
                if (label.Length <= longest.Length) { continue; }
                longest = label;
                where = Path.GetFileName(source) + ":" + (line + 1).ToString();
            }
        }
        #endregion
    }
}
#endif
