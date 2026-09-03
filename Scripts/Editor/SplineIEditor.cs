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
