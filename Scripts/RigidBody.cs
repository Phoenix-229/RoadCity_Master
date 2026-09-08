#region "Imports"
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Strips the <c>Rigidbody</c> off whatever it is attached to, the moment that object wakes.
    /// <c>Scripts/IntersectionObjects.cs:499</c> adds this component to a built intersection object, and the two lines
    /// below are the whole of what it does.
    ///
    /// It used to carry a forced-sleeping experiment as well -- <c>minCollisionVelocity</c>, two commented field
    /// declarations, and a fifty-seven-line <c>/* */</c> block that read all three. RM-66 recorded the field and that
    /// block as ONE disposition group with all four searches negative, and B-21 performed the removal: the field was
    /// serialised into no prefab, scene or asset in the tree, and its only read was inside the comment. What is left
    /// is live, not a stub. </summary>
    public class RigidBody : MonoBehaviour
    {
        private Rigidbody rigidBody;


        private void Awake()
        {
            rigidBody = transform.GetComponent<Rigidbody>();
            DestroyImmediate(rigidBody);
        }
    }
}
