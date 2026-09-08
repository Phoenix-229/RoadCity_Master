// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(SplineC))]
    public class SplineCEditor : Editor
    {
        private SplineC spline;
        private int browseNode = 0;

        /// <summary> B-19. This inspector's own deferred-commit control. The node index reaches
        /// <see cref="browseNode"/> when the author presses Enter or moves focus away, not on the keystroke. Nothing
        /// reacts to the number as it is typed -- the Browse button is what acts on it -- so there is nothing for a
        /// per-keystroke field to buy here, and typing "12" no longer walks through node 1 on the way. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();


        private void OnEnable()
        {
            spline = (SplineC)target;
        }


        public override void OnInspectorGUI()
        {
            fields.BeginFrame();

            #region NodeBrowser
            // The field draws its own label and its own horizontal group; that group nests inside this one, which is
            // what keeps the Browse button on the value's row rather than under it.
            EditorGUILayout.BeginHorizontal();
            fields.IntField("Browse to node:", "splineC#browseNode", ref browseNode,
                "The index of the node to select. Press Browse to select it.");
            if (GUILayout.Button("Browse", GUILayout.Width(80f)))
            {
                // B-19. Every one of these four tests earns its place. The old guard was `browseNode < nodes.Count`
                // alone, which let -1 through into the indexer and threw on the click; a spline that has not built its
                // node list yet has a null `nodes`; and a node destroyed since the list was written leaves a live
                // index pointing at nothing, which would put a null into the selection.
                if (spline != null && spline.nodes != null
                    && browseNode >= 0 && browseNode < spline.nodes.Count
                    && spline.nodes[browseNode] != null)
                {
                    Selection.objects = new Object[1] { spline.nodes[browseNode] };
                }
            }
            EditorGUILayout.EndHorizontal();
            #endregion
        }
    }
}
#endif
