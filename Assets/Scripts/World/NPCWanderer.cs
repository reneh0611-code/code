using CheatOnYourDayOnes.Vehicles;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NPCWanderer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed=1.35f,wanderRadius=10f,turnSpeed=5f,minPause=1.25f,maxPause=4f,gravity=-20f;
        [Header("Vehicle reaction")]
        [SerializeField] private float carAwarenessRadius=7f;
        [SerializeField] private float fleeSpeed=2.25f;
        [SerializeField] private float impactSpeedThreshold=.35f;
        [SerializeField] private float lieDownSeconds=.95f,getUpSeconds=1.35f;
        [SerializeField] private float impactCarryDistance=.45f;
        [SerializeField, Range(-1f,1f)] private float rearHitDotThreshold=-0.25f;
        private static readonly int IdleHash=Animator.StringToHash("Base Layer.Idle"),WalkHash=Animator.StringToHash("Base Layer.Walk"),RunHash=Animator.StringToHash("Base Layer.Run"),FallHash=Animator.StringToHash("Base Layer.Fall"),GettingUpHash=Animator.StringToHash("Base Layer.GettingUp"),FallRearHash=Animator.StringToHash("Base Layer.FallRear"),GettingUpRearHash=Animator.StringToHash("Base Layer.GettingUpRear");
        private CharacterController _controller; private Vector3 _home,_target; private float _pause,_verticalVelocity,_fallUntil,_getUpUntil; private bool _walking,_running,_gettingUp,_rearImpact; private DriveableCar _dangerCar;
        private Vector3 _impactAnchor;
        private void Awake(){_controller=GetComponent<CharacterController>();if(animator==null)animator=GetComponentInChildren<Animator>(true);}
        private void Start(){_home=transform.position;Pause(Random.Range(.4f,2.5f));}
        private void Update(){if(Time.time<_fallUntil){HoldImpactPosition();return;}if(!_gettingUp&&_fallUntil>0&&Time.time>=_fallUntil)StartGettingUp();if(_gettingUp){HoldImpactPosition();if(Time.time>=_getUpUntil){_gettingUp=false;_fallUntil=0;PlayState(IdleHash,.08f);Pause(Random.Range(.15f,.45f));}return;}FindDangerousCar();Vector3 horizontal=Vector3.zero;if(_dangerCar!=null){Vector3 away=transform.position-_dangerCar.transform.position;away.y=0;if(away.sqrMagnitude<.01f)away=transform.right;away.Normalize();transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(away),1f-Mathf.Exp(-turnSpeed*1.45f*Time.deltaTime));horizontal=away*fleeSpeed;SetRunning(true);}else if(_pause>0){SetRunning(false);_pause-=Time.deltaTime;if(_pause<=0)PickTarget();}else{SetRunning(false);Vector3 to=_target-transform.position;to.y=0;if(to.sqrMagnitude<.16f)Pause(Random.Range(minPause,maxPause));else{Vector3 d=to.normalized;transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(d),1f-Mathf.Exp(-turnSpeed*Time.deltaTime));horizontal=d*walkSpeed;SetWalking(true);}}if(_controller.isGrounded&&_verticalVelocity<0)_verticalVelocity=-2;else _verticalVelocity+=gravity*Time.deltaTime;_controller.Move((horizontal+Vector3.up*_verticalVelocity)*Time.deltaTime);}
        private void HoldImpactPosition(){if(!_controller.enabled)return;Vector3 delta=_impactAnchor-transform.position;delta.y=0;if(delta.sqrMagnitude>.0001f)_controller.Move(delta);if(_controller.isGrounded&&_verticalVelocity<0)_verticalVelocity=-2;else _verticalVelocity+=gravity*Time.deltaTime;_controller.Move(Vector3.up*_verticalVelocity*Time.deltaTime);}
        private void StartGettingUp(){_gettingUp=true;_getUpUntil=Time.time+getUpSeconds;int state=_rearImpact?GettingUpRearHash:GettingUpHash;bool played=PlayState(state,.04f);if(!played&&_rearImpact)played=PlayState(GettingUpHash,.04f);Debug.Log($"[CYDOY] NPC GETTING UP: {name} rear={_rearImpact} animation={played}",this);}
        private bool PlayState(int hash,float fade){if(animator==null)animator=GetComponentInChildren<Animator>(true);if(animator==null||animator.runtimeAnimatorController==null||!animator.isActiveAndEnabled)return false;if(!animator.HasState(0,hash))return false;animator.CrossFadeInFixedTime(hash,fade,0,0);return true;}
        private void FindDangerousCar(){_dangerCar=null;float best=carAwarenessRadius;foreach(DriveableCar car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None)){if(car==null||!car.IsOccupied)continue;float d=Vector3.Distance(transform.position,car.transform.position);if(d<best){best=d;_dangerCar=car;}}}
        public bool HitByVehicle(Vector3 carVelocity,Vector3 carPosition){float speed=carVelocity.magnitude;if(speed<impactSpeedThreshold||Time.time<_fallUntil||_gettingUp)return false;Vector3 toCar=carPosition-transform.position;toCar.y=0;if(toCar.sqrMagnitude>.001f)toCar.Normalize();float dot=Vector3.Dot(transform.forward,toCar);_rearImpact=dot<rearHitDotThreshold;Vector3 carry=carVelocity;carry.y=0;if(carry.sqrMagnitude>.001f)carry=carry.normalized*impactCarryDistance;_impactAnchor=transform.position+carry;_fallUntil=Time.time+lieDownSeconds;_getUpUntil=0;_gettingUp=false;_dangerCar=null;_walking=false;_running=false;int state=_rearImpact?FallRearHash:FallHash;bool played=PlayState(state,0f);if(!played&&_rearImpact)played=PlayState(FallHash,0f);Debug.Log($"[CYDOY] NPC HIT IMMEDIATE: {name} speed={speed:F2} rear={_rearImpact} animation={played} anchor={_impactAnchor}",this);return true;}
        private void PickTarget(){Vector2 c=Random.insideUnitCircle*wanderRadius;_target=_home+new Vector3(c.x,0,c.y);SetWalking(true);}
        private void Pause(float duration){_pause=duration;SetRunning(false);SetWalking(false);}
        private void SetRunning(bool running){if(_running==running)return;_running=running;if(running){_walking=false;PlayState(RunHash,.08f);}else if(_dangerCar==null&&_walking)PlayState(WalkHash,.08f);}
        private void SetWalking(bool walking){if(_running)return;if(_walking==walking)return;_walking=walking;PlayState(walking?WalkHash:IdleHash,.1f);}
        public void Configure(float speed,float radius){walkSpeed=speed;wanderRadius=radius;}
    }
}
