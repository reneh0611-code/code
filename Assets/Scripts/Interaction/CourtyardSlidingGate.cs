using CheatOnYourDayOnes.Player;
using UnityEngine;

namespace CheatOnYourDayOnes.Interaction
{
    public sealed class CourtyardSlidingGate : MonoBehaviour, IInteractable
    {
        [SerializeField] Rigidbody panel;
        [SerializeField] BoxCollider safetyZone;
        [SerializeField] Vector3 closedLocalPosition;
        [SerializeField] float travel=6.5f;
        [SerializeField] float duration=2.4f;
        float amount;
        bool targetOpen;
        readonly Collider[] hits=new Collider[64];
        public bool IsOpen=>amount>.99f;
        public void Configure(Rigidbody body,BoxCollider sensor,float distance)
        {panel=body;safetyZone=sensor;travel=distance;closedLocalPosition=panel.transform.localPosition;}
        public string GetInteractionText(PlayerAgent player)=>targetOpen?"Hoftor schließen":"Hoftor öffnen";
        public bool CanInteract(PlayerAgent player)=>player!=null&&panel!=null;
        public void InteractServer(PlayerAgent player){if(CanInteract(player))targetOpen=!targetOpen;}
        void FixedUpdate()
        {
            if(panel==null)return;
            if(!targetOpen&&Occupied())targetOpen=true;
            amount=Mathf.MoveTowards(amount,targetOpen?1:0,Time.fixedDeltaTime/Mathf.Max(.1f,duration));
            float eased=Mathf.SmoothStep(0,1,amount);
            panel.MovePosition(panel.transform.parent.TransformPoint(closedLocalPosition+Vector3.right*(travel*eased)));
        }
        bool Occupied()
        {
            if(safetyZone==null)return true; // Fail open if safety setup is missing.
            var t=safetyZone.transform;var s=t.lossyScale;s=new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
            int count=Physics.OverlapBoxNonAlloc(t.TransformPoint(safetyZone.center),Vector3.Scale(safetyZone.size,s)*.5f,hits,t.rotation,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return true;
            for(int i=0;i<count;i++)
            {
                var c=hits[i];if(c==null||c.transform.IsChildOf(transform))continue;
                if(c.GetComponentInParent<CharacterController>()!=null||c.GetComponentInParent<PlayerAgent>()!=null||c.attachedRigidbody!=null&&!c.attachedRigidbody.isKinematic)return true;
            }
            return false;
        }
    }
}
