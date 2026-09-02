using UnityEngine;

namespace CheatOnYourDayOnes.Vehicles
{
    public sealed class ParkingSlotMarker : MonoBehaviour
    {
        [SerializeField] private float width=2.65f;
        [SerializeField] private float length=5.25f;
        public float Width=>width;
        public float Length=>length;

        private void OnDrawGizmos()
        {
            Gizmos.color=new Color(.2f,.8f,1f,.55f);
            Matrix4x4 old=Gizmos.matrix;
            Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up*.04f,new Vector3(width,.08f,length));
            Gizmos.DrawLine(Vector3.zero,Vector3.forward*(length*.42f));
            Gizmos.matrix=old;
        }
    }
}
