// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(SplineF))]
    public class SplineFEditor : Editor
    {
        private SplineF splineF;


        private void OnEnable()
        {
            splineF = (SplineF)target;
        }


        public override void OnInspectorGUI()
        {
            //Intentionally left empty.
        }
    }
}
#endif
