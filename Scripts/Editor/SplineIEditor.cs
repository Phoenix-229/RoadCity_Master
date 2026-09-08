// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(SplineI))]
    public class SplineIEditor : Editor
    {
        private SplineI splineI;


        private void OnEnable()
        {
            splineI = (SplineI)target;
        }


        public override void OnInspectorGUI()
        {
            //Intentionally left empty.
        }
    }
}
#endif
