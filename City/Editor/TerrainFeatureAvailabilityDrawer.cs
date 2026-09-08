// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> The inspector's half of § 10.24: a control that needs a provider is greyed out and SAYS why.
    ///
    /// This is the third consumer of the availability table -- the manual and Universe Creator's documentation render
    /// <see cref="TerrainFeatureAvailability.ToMarkdown"/>, and every inspector control that touches terrain wraps
    /// itself in <see cref="BeginFeature"/> here. That is the whole point of the table being data: a greyed control
    /// and the sentence explaining it come from the same row, so they cannot disagree, and a feature cannot be quietly
    /// disabled without the reason appearing next to it.
    ///
    /// The mode is read from the installed gateway rather than passed in, because the gateway is the one place the
    /// decision lives. Nothing here scans the scene; <see cref="TerrainGatewayBootstrap"/> does that, once. </summary>
    public static class TerrainFeatureAvailabilityDrawer
    {
        /// <summary> The mode currently in force. </summary>
        public static TerrainProviderMode Mode { get { return TerrainWriteGateway.Active.Mode; } }

        /// <summary> Wrap a control that depends on a terrain feature. Returns whether the feature is usable; when it
        /// is not, the control is disabled AND the reason is drawn, so there is no silent grey. Always pair with
        /// <see cref="EndFeature"/>. </summary>
        public static bool BeginFeature(string featureId)
        {
            TerrainProviderMode mode = Mode;
            bool available = TerrainFeatureAvailability.AvailableIn(featureId, mode);
            if (!available)
            {
                EditorGUILayout.HelpBox(TerrainFeatureAvailability.SentenceFor(featureId, mode) + ".",
                    MessageType.Info);
            }
            EditorGUI.BeginDisabledGroup(!available);
            return available;
        }

        public static void EndFeature()
        {
            EditorGUI.EndDisabledGroup();
        }

        /// <summary> The sentence for one feature in the current mode, for a tooltip or a label. </summary>
        public static string Explain(string featureId)
        {
            return TerrainFeatureAvailability.SentenceFor(featureId, Mode);
        }

        /// <summary> The header a terrain-touching inspector shows once: which mode, decided from what, and what that
        /// costs. Standalone is stated as a working mode with a shorter feature list, not as an error. </summary>
        public static void DrawModeHeader()
        {
            TerrainProviderMode mode = Mode;
            string decision = TerrainGatewayBootstrap.Decided
                ? TerrainGatewayBootstrap.LastDecision
                : "The scene has not been checked for a terrain provider yet.";

            EditorGUILayout.HelpBox(decision, MessageType.Info);

            IList<TerrainFeatureRow> missing = TerrainFeatureAvailability.UnavailableIn(mode);
            for (int i = 0; i < missing.Count; i++)
            {
                EditorGUILayout.LabelField("• " + missing[i].title, EditorStyles.wordWrappedMiniLabel);
            }

            if (GUILayout.Button("Check this scene for a terrain provider"))
            {
                TerrainGatewayBootstrap.Refresh();
            }
        }

        /// <summary> The whole table, for a help foldout. Same rows, same order, same sentences as the documentation.
        /// </summary>
        public static void DrawTable()
        {
            TerrainProviderMode mode = Mode;
            for (int i = 0; i < TerrainFeatureAvailability.Count; i++)
            {
                TerrainFeatureRow row = TerrainFeatureAvailability.Row(i);
                bool available = row.AvailableIn(mode);
                EditorGUILayout.LabelField(available ? row.title : row.title + "  (needs a provider)",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(row.Sentence(mode) + ".", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
#endif
