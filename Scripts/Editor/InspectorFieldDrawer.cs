#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-51's bridge from the metadata table to a drawn field. A rebuilt inspector calls
    /// <see cref="Draw"/> per field: it draws the serialised property with the unit appended to its label, then -- if
    /// the current value is outside the field's real-world range -- shows an inline HelpBox naming the limit and its
    /// source, rather than clamping silently. This keeps the registry live (RM-66: wired, not dead) while the full
    /// foldout layout is finished in the scene half. It never renames a serialised field (the non-breaking contract):
    /// it looks the property up by its verbatim name and only changes how it is presented. </summary>
    public static class InspectorFieldDrawer
    {
        /// <summary> Draw one descriptor's serialised property with a unit-suffixed label and inline range validation.
        /// Returns true if the property was found and drawn.
        ///
        /// <paramref name="fields"/> is the calling inspector's own <see cref="FocusOutFields"/>, and it carries B-19's
        /// commit rule: see the overload below for what it does and what a null does instead. </summary>
        public static bool Draw(SerializedObject so, FieldDescriptor d, FocusOutFields fields)
        {
            return Draw(so, d, fields, null);
        }


        /// <summary> The same field, with <paramref name="trailing"/> drawn on the value's own row, to its right.
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

            string unit = d.UnitLabel();
            string label = string.IsNullOrEmpty(unit) ? d.label : d.label + "  (" + unit + ")";
            GUIContent content = new GUIContent(label);
            if (trailing == null)
            {
                DrawValue(prop, content, fields);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                DrawValue(prop, content, fields);
                trailing();
                EditorGUILayout.EndHorizontal();
            }

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
