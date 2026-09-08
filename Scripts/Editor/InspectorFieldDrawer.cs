#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-51's bridge from the metadata table to a drawn field. A rebuilt inspector calls
    /// <see cref="Draw"/> per field: it draws the serialised property with its unit in a column of its own to the right
    /// of the value and the field's own sentence on the hover, then -- if the current value is outside the field's
    /// real-world range -- shows an inline HelpBox naming the limit and its source, rather than clamping silently. This
    /// keeps the registry live (RM-66: wired, not dead) while the full foldout layout is finished in the scene half. It
    /// never renames a serialised field (the non-breaking contract): it looks the property up by its verbatim name and
    /// only changes how it is presented.
    ///
    /// B-5, fault 5, moved the unit out of the label. `"Lane width  (m)"` put the unit inside the PREFIX LABEL, where it
    /// competed with the field's name for the same column and pushed the longest labels onto a second line -- the exact
    /// overflow B-4 had just widened that column to stop. A unit is not part of a field's name; it annotates the value.
    /// It is drawn beside the value now, at one shared width
    /// (<see cref="InspectorTextLayout.UnitColumnWidth"/>), so the tokens line up down the inspector instead of ending
    /// wherever each label happens to end. Fault 8 put the descriptor's tooltip on the same GUIContent, which is the
    /// only place an author can be told what a field DOES without spending a row on it. </summary>
    public static class InspectorFieldDrawer
    {
        /// <summary> Draw one descriptor's serialised property with its unit in its own column, its own sentence on the
        /// hover, and inline range validation. Returns true if the property was found and drawn.
        ///
        /// <paramref name="fields"/> is the calling inspector's own <see cref="FocusOutFields"/>, and it carries B-19's
        /// commit rule: see the overload below for what it does and what a null does instead. </summary>
        public static bool Draw(SerializedObject so, FieldDescriptor d, FocusOutFields fields)
        {
            return Draw(so, d, fields, null);
        }


        /// <summary> The same field, with <paramref name="trailing"/> drawn on the value's own row, to the right of the
        /// unit column.
        ///
        /// RM-102 added this for the legacy inspectors, and the reason is a small piece of accumulated usability that
        /// would otherwise have been lost in the move onto this drawer: each numeric field in those inspectors carried
        /// a narrow icon button beside it that put the value back to its default. An author who has widened a lane to
        /// something wrong and cannot remember what it was needs that button, and it has to be on the value's row --
        /// a reset control a row below the thing it resets reads as belonging to the next field.
        ///
        /// The out-of-range message is deliberately raised OUTSIDE the row: a HelpBox inside a horizontal group is
        /// squeezed into whatever width is left beside the value, which is where a long sentence naming a limit and
        /// its source becomes one unreadable line. Drawn after the row it spans the inspector, as it does everywhere
        /// else.
        ///
        /// B-19 put <paramref name="fields"/> here. This method is the single funnel every registry field goes through,
        /// so the commit rule belongs at this one call rather than at each inspector: a number typed here reaches the
        /// property when the author presses Enter or moves focus away, not on the keystroke. A null <paramref
        /// name="fields"/> falls back to Unity's own drawer, which reports per keystroke -- the behaviour this task
        /// exists to remove, kept only so the fallback is a documented path rather than a crash, and forbidden at every
        /// call site by <see cref="LegacyInspectorGate"/>. </summary>
        public static bool Draw(SerializedObject so, FieldDescriptor d, FocusOutFields fields, System.Action trailing)
        {
            if (so == null || d == null) { return false; }
            SerializedProperty prop = so.FindProperty(d.field);
            if (prop == null) { return false; }

            // Every row opens a horizontal group now, with or without a trailing control, because the unit column is
            // always there. The two paths this replaces differed only in that group; keeping them apart would have left
            // two places to change the column in.
            GUIContent content = new GUIContent(d.label, d.tooltip);
            EditorGUILayout.BeginHorizontal();
            DrawValue(prop, content, fields);
            DrawUnit(d);
            if (trailing != null) { trailing(); }
            EditorGUILayout.EndHorizontal();

            if (d.range.IsBounded && TryNumeric(prop, out double value))
            {
                ValidationResult r = d.Validate(value);
                if (!r.ok) { EditorGUILayout.HelpBox(r.message, MessageType.Warning); }
            }

            // RM-93. A second, independent check for the fields whose limit is not a property of the field alone.
            // It runs even when the flat range passed, because the two ask different questions: 130 km/h is inside
            // the posted-speed range every road shares AND above what an urban two-lane street was dimensioned for.
            ValidationResult cross = CrossField(so, d);
            if (!cross.ok) { EditorGUILayout.HelpBox(cross.message, MessageType.Warning); }
            return true;
        }


        /// <summary> The one place a registry field's value control is drawn. Two lines, and both of them matter:
        /// with an inspector's <see cref="FocusOutFields"/> the value commits on Enter or focus-out, and without one it
        /// commits per keystroke because that is what Unity's drawer does.
        ///
        /// Kept as its own method rather than inlined twice because the trailing-control overload draws the same value
        /// inside a horizontal group, and a rule that has to be written twice is a rule that gets changed once. </summary>
        private static void DrawValue(SerializedProperty prop, GUIContent content, FocusOutFields fields)
        {
            if (fields != null) { fields.Property(prop, content); }
            else { EditorGUILayout.PropertyField(prop, content); }
        }


        /// <summary> B-5, fault 5. The registry path into the unit column: a descriptor knows its own token and its own
        /// sentence, so it needs no arguments beyond itself.
        ///
        /// The tooltip is passed on only when there is a token to hover. On a unitless field the cell is blank, and a
        /// tooltip that appeared over blank space would be a hover with nothing visible to attribute it to -- which is
        /// why the empty check lives in <see cref="Unit"/> rather than being repeated at each caller. </summary>
        private static void DrawUnit(FieldDescriptor d)
        {
            Unit(d.UnitLabel(), d.tooltip);
        }


        /// <summary> The unit column itself: one fixed-width cell to the right of a value, holding a unit token or
        /// nothing. Public because the rebuilt legacy inspectors have numeric controls that are NOT registry fields --
        /// the terrain clearing widths, the editor camera's speed -- and a second cell written beside them would be a
        /// second place the column width could drift. They reserve the same cell from this one method.
        ///
        /// The cell is reserved even when the token is empty, and that is the point of it rather than an oversight. A
        /// column that collapsed on the unitless fields -- a lane count, a dropdown -- would leave every reset button in
        /// the inspector at a different distance from the left, so the eye could not run down the column of buttons the
        /// way it can down the column of values. An empty cell costs one blank rect and keeps both columns straight.
        ///
        /// `EditorStyles.miniLabel` because a unit annotates a value and should not read as loud as the value: the same
        /// relationship Unity's own inspectors draw with it. The style is READ, never written -- B-3's fault was writing
        /// to instances the editor owns, and <see cref="InspectorStyleOwnershipGate"/> is what holds that line. </summary>
        public static void Unit(string token, string tooltip)
        {
            GUIContent content = string.IsNullOrEmpty(token)
                ? GUIContent.none
                : new GUIContent(token, tooltip);
            GUILayout.Label(content, EditorStyles.miniLabel,
                GUILayout.Width(InspectorTextLayout.UnitColumnWidth()));
        }


        /// <summary> Run the cross-field rule that applies to this field, if any. The rule itself lives in
        /// <see cref="SectionRules"/> -- all that happens here is reading the OTHER field the rule needs out of the
        /// same SerializedObject, so the drawer stays generic and does not need a typed Road reference (which would
        /// stop it from being usable on a multi-object selection). A field with no cross-field rule returns ok. </summary>
        private static ValidationResult CrossField(SerializedObject so, FieldDescriptor d)
        {
            if (d.component != "Road") { return ValidationResult.Ok(); }

            if (d.field == "lanesBackward")
            {
                SerializedProperty back = so.FindProperty("lanesBackward");
                SerializedProperty oneWay = so.FindProperty("isOneWay");
                if (back == null || oneWay == null) { return ValidationResult.Ok(); }
                return SectionRules.LanesBackward((int)back.longValue, oneWay.boolValue);
            }

            if (d.field == "speedLimitKPH")
            {
                SerializedProperty speed = so.FindProperty("speedLimitKPH");
                SerializedProperty cls = so.FindProperty("roadClass");
                if (speed == null || cls == null) { return ValidationResult.Ok(); }
                // enumValueIndex is the position in the DECLARED order, which is what the enum's underlying value is
                // here: RoadClassEnum assigns no explicit numbers, and RoadClassDefaults indexes its rows the same way.
                int index = cls.enumValueIndex;
                Road.RoadClassEnum[] classes = (Road.RoadClassEnum[])System.Enum.GetValues(typeof(Road.RoadClassEnum));
                if (index < 0 || index >= classes.Length) { return ValidationResult.Ok(); }
                return SectionRules.DesignSpeed(speed.doubleValue, classes[index]);
            }

            return ValidationResult.Ok();
        }

        /// <summary> Read a numeric serialised value as a double, whatever its integer/float type. </summary>
        private static bool TryNumeric(SerializedProperty prop, out double value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    value = prop.doubleValue; return true;
                case SerializedPropertyType.Integer:
                    value = prop.longValue; return true;
                default:
                    value = 0.0; return false;
            }
        }
    }
}
#endif
