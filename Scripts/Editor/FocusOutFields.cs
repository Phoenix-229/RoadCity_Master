// -----------------------------------------------------------------------------
//  Road & City Master
//  File:    FocusOutFields.cs
//  Purpose: Input controls that commit on Enter or focus-out, and sliders that
//           commit on drag-release. Nothing here commits per keystroke.
//
//  WHY THIS EXISTS
//  ---------------
//  A committed value on this tool's inspectors asks for a road rebuild. Unity's
//  own EditorGUILayout.Slider and PropertyField report a change on every mouse
//  move and every character typed, so dragging one slider across its span asked
//  for a build per frame, and typing "12.5" into a width asked for four -- three
//  of them against a half-typed number ("1", "12", "12."). The dispatcher
//  supersedes those rather than running them all, so the cost showed up as an
//  editor that would not keep up with the drag rather than as a queue of builds,
//  but the request was still wrong at the source.
//
//  So the value under the control is left alone while it is being edited, and
//  replaced only when the author signals they are done. Escape abandons the edit
//  and leaves the committed value where it was.
//
//  Instance state, not static: BeginFrame() belongs to one draw pass of one
//  inspector, and two inspectors drawing in the same frame -- a Road and the
//  RoadSystem above it, both visible in a locked second inspector -- each need
//  their own pending buffer.
//
//  DELIBERATE DUPLICATION
//  ----------------------
//  Universe Creator has a class of the same name and the same shape at
//  Core/Editor/Utilities/FocusOutFields.cs. This is a copy, not a reference:
//  Road & City Master ships as its own product from its own repository and
//  cannot compile against UniverseCreator.Editor. The shape is copied on purpose
//  because the shape is what makes it safe, and an audit that finds two of these
//  should read this paragraph rather than delete one.
// -----------------------------------------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace RoadCityMaster
{
    /// <summary> Deferred-commit input controls for this tool's inspectors. One instance per Editor, reset with
    /// <see cref="BeginFrame"/> at the top of each draw pass. </summary>
    public class FocusOutFields
    {
        #region "What is mid-edit"
        // Which control owns the pending buffer, and what is in it. One slot, not a map: Unity focuses one control at
        // a time across the whole editor, and the names below are built from a target's instance id, so two inspectors
        // cannot name the same field.
        private string focusedField = "";
        private string pendingText = "";

        // A slider mid-drag: its name and the value under the handle, which is not the value on the component yet.
        private string liveSliderField;
        private float liveSliderValue;
        #endregion


        /// <summary> True on a frame where a control committed a new value. Read it once per pass, after drawing. </summary>
        public bool Changed { get; private set; }


        /// <summary> Call once at the top of a draw pass, before any control. </summary>
        public void BeginFrame() { Changed = false; }

        #region "The core: a text box that reports only on commit"
        /// <summary> Returns the new text on the frame the author commits it -- Enter, or focus moving away -- and null
        /// on every other frame, including every frame while they are typing. That return value is the whole contract;
        /// everything else in this file is a typed wrapper around it.
        ///
        /// <paramref name="fieldName"/> has to be unique across the editor, because Unity's focus is. Use
        /// <see cref="KeyFor"/> for a serialized property; for a plain field, prefix the component's instance id. </summary>
        public string TextBox(string fieldName, string committedText, params GUILayoutOption[] options)
        {
            string commit = null;
            bool hasFocus = GUI.GetNameOfFocusedControl() == fieldName;

            // Focus-out: this control had focus on the last pass and does not now. Nothing tells a control it lost
            // focus, so noticing the change between two passes is the only way to catch it.
            if (!hasFocus && focusedField == fieldName)
            {
                commit = pendingText;
                focusedField = "";
                pendingText = "";
            }

            // Focus-in: seed the buffer from the live value, so the author edits what is actually on the component.
            if (hasFocus && focusedField != fieldName)
            {
                focusedField = fieldName;
                pendingText = committedText;
            }

            GUI.SetNextControlName(fieldName);

            if (hasFocus)
            {
                pendingText = EditorGUILayout.TextField(pendingText, options);

                Event e = Event.current;
                if (e.type == EventType.KeyDown
                    && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                {
                    commit = pendingText;
                    focusedField = "";
                    pendingText = "";
                    GUI.FocusControl(null);
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    // Abandoned, not committed: the buffer is dropped and the committed value is what gets drawn on
                    // the next pass. Without this, Escape would leave the last keystroke applied.
                    focusedField = "";
                    pendingText = "";
                    GUI.FocusControl(null);
                    e.Use();
                }
            }
            else
            {
                // Not focused: draw the real value, and let Unity's own focus handling take the click.
                EditorGUILayout.TextField(committedText, options);
            }

            return commit;
        }
        #endregion


        #region "Typed wrappers over plain fields"
        /// <summary> A labelled number box. Returns true on the pass where a new value was committed. </summary>
        public bool FloatField(string label, string fieldName, ref float value, string tooltip,
            string format = "0.###")
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
            string commit = TextBox(fieldName, value.ToString(format));
            EditorGUILayout.EndHorizontal();

            if (commit == null) { return false; }

            float parsed;
            if (!float.TryParse(commit, out parsed)) { return false; }
            if (Mathf.Approximately(parsed, value)) { return false; }

            value = parsed;
            Changed = true;
            return true;
        }


        /// <summary> A labelled whole-number box. </summary>
        public bool IntField(string label, string fieldName, ref int value, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
            string commit = TextBox(fieldName, value.ToString());
            EditorGUILayout.EndHorizontal();

            if (commit == null) { return false; }

            // Parsed as a double first: "9999999999" is a number an author can type, and int.TryParse would reject it
            // outright rather than pinning it to something the field can hold.
            double parsed;
            if (!double.TryParse(commit, out parsed)) { return false; }
            parsed = System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, parsed));
            if ((int)parsed == value) { return false; }

            value = (int)parsed;
            Changed = true;
            return true;
        }


        /// <summary> A labelled text box. </summary>
        public bool StringField(string label, string fieldName, ref string value, string tooltip)
        {
            if (value == null) { value = string.Empty; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
            string commit = TextBox(fieldName, value);
            EditorGUILayout.EndHorizontal();

            if (commit == null || commit == value) { return false; }

            value = commit;
            Changed = true;
            return true;
        }
        #endregion


        #region "Sliders, which commit when the handle is let go"
        /// <summary> A slider with a number beside it. The handle moves freely while dragged and commits once, when the
        /// mouse button comes up; the number box beside it commits on Enter or focus-out like any other.
        ///
        /// A typed number is NOT clamped to the slider's range. The range is the span that is useful to drag through,
        /// not a limit on what the maths accepts, and silently rewriting a number an author deliberately typed is worse
        /// than a handle that sits at one end. A dragged value is clamped, because the handle cannot leave the bar. </summary>
        public bool FloatSlider(string label, string fieldName, ref float value, float min, float max, string tooltip,
            string format = "0.###")
        {
            if (max < min) { max = min; }

            bool dragging = liveSliderField == fieldName;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));

            EditorGUI.BeginChangeCheck();
            float next = GUILayout.HorizontalSlider(dragging ? liveSliderValue : value, min, max,
                GUILayout.MinWidth(40));
            if (EditorGUI.EndChangeCheck())
            {
                liveSliderField = fieldName;
                liveSliderValue = next;
                dragging = true;
            }

            if (dragging)
            {
                // A greyed label rather than an editable field, so the number reads as "where the handle is" and not as
                // "what the road is built from" -- because until the button comes up, it is not.
                GUILayout.Label(liveSliderValue.ToString(format), EditorStyles.miniLabel, GUILayout.Width(56));
                EditorGUILayout.EndHorizontal();

                // hotControl drops to 0 the instant the mouse button is released, wherever the pointer ended up.
                if (GUIUtility.hotControl == 0)
                {
                    float committed = Mathf.Clamp(liveSliderValue, min, max);
                    liveSliderField = null;

                    if (!Mathf.Approximately(committed, value))
                    {
                        value = committed;
                        Changed = true;
                        return true;
                    }
                }
                return false;
            }

            string commit = TextBox(fieldName + "#num", value.ToString(format), GUILayout.Width(56));
            EditorGUILayout.EndHorizontal();

            if (commit == null) { return false; }

            float parsed;
            if (!float.TryParse(commit, out parsed)) { return false; }
            if (Mathf.Approximately(parsed, value)) { return false; }

            value = parsed;
            Changed = true;
            return true;
        }


        /// <summary> The whole-number slider. Same commit rule; the handle snaps to integers as it moves. </summary>
        public bool IntSlider(string label, string fieldName, ref int value, int min, int max, string tooltip)
        {
            if (max < min) { max = min; }

            bool dragging = liveSliderField == fieldName;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));

            EditorGUI.BeginChangeCheck();
            float next = GUILayout.HorizontalSlider(dragging ? liveSliderValue : value, min, max,
                GUILayout.MinWidth(40));
            if (EditorGUI.EndChangeCheck())
            {
                liveSliderField = fieldName;
                liveSliderValue = Mathf.Round(next);
                dragging = true;
            }

            if (dragging)
            {
                GUILayout.Label(Mathf.RoundToInt(liveSliderValue).ToString(), EditorStyles.miniLabel,
                    GUILayout.Width(56));
                EditorGUILayout.EndHorizontal();

                if (GUIUtility.hotControl == 0)
                {
                    int committed = Mathf.Clamp(Mathf.RoundToInt(liveSliderValue), min, max);
                    liveSliderField = null;

                    if (committed != value)
                    {
                        value = committed;
                        Changed = true;
                        return true;
                    }
                }
                return false;
            }

            string commit = TextBox(fieldName + "#num", value.ToString(), GUILayout.Width(56));
            EditorGUILayout.EndHorizontal();

            if (commit == null) { return false; }

            double parsed;
            if (!double.TryParse(commit, out parsed)) { return false; }
            parsed = System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, parsed));
            if ((int)parsed == value) { return false; }

            value = (int)parsed;
            Changed = true;
            return true;
        }
        #endregion


        #region "A vector, one axis at a time"
        /// <summary> Three number boxes on one row. Each axis commits on its own, so tabbing from X to Y commits X --
        /// which is the behaviour an author expects from a position field and is why the axes are separate controls
        /// rather than one Unity Vector3Field. </summary>
        public bool Vector3Field(string label, string fieldName, ref Vector3 value, string tooltip,
            string format = "0.###")
        {
            Vector3 next = value;
            bool committed = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));

            GUILayout.Label("X", EditorStyles.miniLabel, GUILayout.Width(12));
            string cx = TextBox(fieldName + "#x", value.x.ToString(format));
            GUILayout.Label("Y", EditorStyles.miniLabel, GUILayout.Width(12));
            string cy = TextBox(fieldName + "#y", value.y.ToString(format));
            GUILayout.Label("Z", EditorStyles.miniLabel, GUILayout.Width(12));
            string cz = TextBox(fieldName + "#z", value.z.ToString(format));

            EditorGUILayout.EndHorizontal();

            float parsed;
            if (cx != null && float.TryParse(cx, out parsed) && !Mathf.Approximately(parsed, value.x))
            {
                next.x = parsed;
                committed = true;
            }
            if (cy != null && float.TryParse(cy, out parsed) && !Mathf.Approximately(parsed, value.y))
            {
                next.y = parsed;
                committed = true;
            }
            if (cz != null && float.TryParse(cz, out parsed) && !Mathf.Approximately(parsed, value.z))
            {
                next.z = parsed;
                committed = true;
            }

            if (!committed) { return false; }

            value = next;
            Changed = true;
            return true;
        }
        #endregion


        #region "Controls with nothing to defer"
        // A tick, a menu choice, a colour or an object reference is one discrete decision: there is no half-typed state
        // to protect, and deferring them would only make them feel broken. They live here so that a caller can read one
        // Changed flag for its whole pass rather than mixing this file's result with its own change checks.

        /// <summary> A tick box. Commits on the click. </summary>
        public bool Toggle(string label, ref bool value, string tooltip)
        {
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.Toggle(new GUIContent(label, tooltip), value);
            if (!EditorGUI.EndChangeCheck() || next == value) { return false; }

            value = next;
            Changed = true;
            return true;
        }


        /// <summary> A menu of enum values. Commits on the pick. </summary>
        public bool EnumField<T>(string label, ref T value, string tooltip) where T : struct, System.Enum
        {
            EditorGUI.BeginChangeCheck();
            System.Enum next = EditorGUILayout.EnumPopup(new GUIContent(label, tooltip), (System.Enum)(object)value);
            if (!EditorGUI.EndChangeCheck()) { return false; }

            T picked = (T)next;
            if (picked.Equals(value)) { return false; }

            value = picked;
            Changed = true;
            return true;
        }


        /// <summary> An asset or component reference. Commits on the drop. </summary>
        public bool ObjectField<T>(string label, ref T value, bool allowSceneObjects, string tooltip)
            where T : Object
        {
            EditorGUI.BeginChangeCheck();
            Object next = EditorGUILayout.ObjectField(new GUIContent(label, tooltip), value, typeof(T),
                allowSceneObjects);
            if (!EditorGUI.EndChangeCheck() || next == (Object)value) { return false; }

            value = next as T;
            Changed = true;
            return true;
        }
        #endregion


        #region "The bridge to a serialized property"
        /// <summary> A control name that cannot collide. Unity's focused-control name is one string for the whole
        /// editor, so a name of "laneWidth" would make two Road inspectors -- one of them a locked second window --
        /// share a pending buffer, and typing into either would show in both. The target's entity id is unique for
        /// the session, and the property path is unique within a target.
        ///
        /// `GetEntityId` and not the older per-object id call, for the reason <see cref="Road.BuildOwnerId"/> already
        /// records: Unity 6 made that one obsolete-as-error, and this project's forbidden-API list carries it too, so
        /// the older spelling fails the compile and then fails the audit. What is wanted here is exactly what an
        /// entity id is: unique while the object lives, meaningless across sessions, and never persisted -- this one
        /// becomes a GUI control name for the length of one editor session and nothing more.
        ///
        /// A destroyed target gets its own bucket rather than an exception: an inspector can be repainted once after
        /// its target is gone, and a throw on that frame would take the whole inspector down. </summary>
        public static string KeyFor(SerializedProperty prop)
        {
            if (prop == null) { return "?"; }
            Object target = prop.serializedObject != null ? prop.serializedObject.targetObject : null;
            if (target == null) { return "0:" + prop.propertyPath; }
            return target.GetEntityId().ToString() + ":" + prop.propertyPath;
        }


        /// <summary> Draws one serialized property with this file's commit rule where the kind of value has a
        /// half-typed state to protect, and Unity's own drawer where it does not.
        ///
        /// Returns true on the pass where a value was committed. The property is written through
        /// <see cref="SerializedProperty"/> rather than onto the object, so the caller's ApplyModifiedProperties still
        /// produces the single undo step that <c>LegacyInspectorGate</c>'s case 7 asks for -- and produces ONE per
        /// commit rather than one per keystroke, which is the part that was wrong before. </summary>
        public bool Property(SerializedProperty prop, GUIContent label)
        {
            if (prop == null) { return false; }

            string key = KeyFor(prop);
            string text = label != null ? label.text : prop.displayName;
            string tip = label != null ? label.tooltip : string.Empty;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    // doubleValue on a float-backed property returns the float widened, and assigning it narrows back.
                    // Going through a float keeps the round trip exact for the Approximately test in the wrapper.
                    float value = prop.floatValue;
                    if (!FloatField(text, key, ref value, tip)) { return false; }
                    prop.floatValue = value;
                    return true;
                }

                case SerializedPropertyType.Integer:
                {
                    int value = prop.intValue;
                    if (!IntField(text, key, ref value, tip)) { return false; }
                    prop.intValue = value;
                    return true;
                }

                case SerializedPropertyType.String:
                {
                    string value = prop.stringValue;
                    if (!StringField(text, key, ref value, tip)) { return false; }
                    prop.stringValue = value;
                    return true;
                }

                case SerializedPropertyType.Vector3:
                {
                    Vector3 value = prop.vector3Value;
                    if (!Vector3Field(text, key, ref value, tip)) { return false; }
                    prop.vector3Value = value;
                    return true;
                }

                default:
                {
                    // A tick, a menu, a colour, a reference, or anything with children: nothing to defer, and Unity's
                    // drawer is the correct one to use. The change check is what keeps the caller's single Changed flag
                    // honest for the whole pass.
                    EditorGUI.BeginChangeCheck();
                    if (label != null) { EditorGUILayout.PropertyField(prop, label, true); }
                    else { EditorGUILayout.PropertyField(prop, true); }
                    if (!EditorGUI.EndChangeCheck()) { return false; }

                    Changed = true;
                    return true;
                }
            }
        }
        /// <summary> A slider over a serialized float. The property is written only on the pass where the value is
        /// committed, so a caller that follows this with ApplyModifiedProperties gets one undo step per commit. </summary>
        public bool FloatSlider(SerializedProperty prop, string label, float min, float max, string tooltip = null)
        {
            if (prop == null) { return false; }
            float value = prop.floatValue;
            if (!FloatSlider(label, KeyFor(prop), ref value, min, max, tooltip)) { return false; }
            prop.floatValue = value;
            return true;
        }


        /// <summary> A number box over a serialized float, with no slider and therefore no range at all. </summary>
        public bool FloatField(SerializedProperty prop, string label, string tooltip = null)
        {
            if (prop == null) { return false; }
            float value = prop.floatValue;
            if (!FloatField(label, KeyFor(prop), ref value, tooltip)) { return false; }
            prop.floatValue = value;
            return true;
        }


        /// <summary> A slider over a serialized int. </summary>
        public bool IntSlider(SerializedProperty prop, string label, int min, int max, string tooltip = null)
        {
            if (prop == null) { return false; }
            int value = prop.intValue;
            if (!IntSlider(label, KeyFor(prop), ref value, min, max, tooltip)) { return false; }
            prop.intValue = value;
            return true;
        }


        /// <summary> A number box over a serialized int. </summary>
        public bool IntField(SerializedProperty prop, string label, string tooltip = null)
        {
            if (prop == null) { return false; }
            int value = prop.intValue;
            if (!IntField(label, KeyFor(prop), ref value, tooltip)) { return false; }
            prop.intValue = value;
            return true;
        }


        /// <summary> A tick box over a serialized bool. Immediate -- a click is the commit -- and here so that a tick
        /// raises <see cref="Changed"/> alongside the deferred controls, which is what lets one inspector decide from
        /// one flag whether its pass asked for anything. </summary>
        public bool Toggle(SerializedProperty prop, string label, string tooltip = null)
        {
            if (prop == null) { return false; }
            bool value = prop.boolValue;
            if (!Toggle(label, ref value, tooltip)) { return false; }
            prop.boolValue = value;
            return true;
        }
        #endregion
    }
}
#endif
