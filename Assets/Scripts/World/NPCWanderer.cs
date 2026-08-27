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
        [SerializeField] private float carAwarenessRadius=7f,fleeSpeed=2.25f,fleeOnlyAboveKmh=30f,impactSpeedThreshold=.20f;
        [SerializeField] private float minimumLieSeconds=1.25f,impactCarryDistance=.20f;
        [SerializeField] private float groundRayHeight=6f,groundRayDistance=20f;
        [SerializeField] private float carClearanceBeforeGetUp=3.2f,extraWaitAfterCarClears=.65f,maxAnimationFallTravel=4f;
        [SerializeField,Range(-1f,1f)] private float rearHitDotThreshold=-.25f;
        [Header("Player punch reaction")]
        [SerializeField] private float punchReactionSeconds=.42f;
        [SerializeField] private float fleeAfterPunchSeconds=6f;
        [SerializeField] private float punchFleeSpeed=3.6f;

        private const float ForcedBodyGroundClearance = -.100f;
        private const float ClipFinishedNormalizedTime = .985f;

        private static readonly int IdleHash=Animator.StringToHash("Base Layer.Idle"),WalkHash=Animator.StringToHash("Base Layer.Walk"),RunHash=Animator.StringToHash("Base Layer.Run"),FallHash=Animator.StringToHash("Base Layer.Fall"),GettingUpHash=Animator.StringToHash("Base Layer.GettingUp"),FallRearHash=Animator.StringToHash("Base Layer.FallRear"),GettingUpRearHash=Animator.StringToHash("Base Layer.GettingUpRear"),Hit1Hash=Animator.StringToHash("Base Layer.Hit1"),Hit2Hash=Animator.StringToHash("Base Layer.Hit2");
        private CharacterController _controller; private Vector3 _home,_target,_impactAnchor,_fallOriginAnchor,_fallBodyStartCenter;
        private float _pause,_verticalVelocity,_fallEarliestGetUp,_safeGetUpAfter; private bool _walking,_running,_gettingUp,_rearImpact,_fallMotionTracking;
        private DriveableCar _dangerCar; private SkinnedMeshRenderer _mainSkinnedMesh; private Collider[] _allColliders; private bool[] _colliderStates;
        private Transform _meleeAttacker,_visualRoot; private float _meleeReactionUntil,_forcedFleeUntil,_meleeReactionStarted; private int _meleeHitVariant; private Quaternion _visualBaseRotation;

        public bool IsDown=>_fallEarliestGetUp>0f||_gettingUp;
        public Vector3 DownPosition=>_impactAnchor;

        private void Awake(){_controller=GetComponent<CharacterController>();if(animator==null)animator=GetComponentInChildren<Animator>(true);if(animator!=null){animator.applyRootMotion=false;_visualRoot=animator.transform;_visualBaseRotation=_visualRoot.localRotation;}CacheBody();CacheColliders();}
        private void CacheBody(){float best=-1f;foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(skin==null)continue;Vector3 s=skin.bounds.size;float v=Mathf.Abs(s.x*s.y*s.z);if(v>best){best=v;_mainSkinnedMesh=skin;}}if(_mainSkinnedMesh!=null)_mainSkinnedMesh.updateWhenOffscreen=true;}
        private void CacheColliders(){_allColliders=GetComponentsInChildren<Collider>(true);_colliderStates=new bool[_allColliders.Length];}
        private void Start(){_home=transform.position;Pause(Random.Range(.4f,2.5f));}

        private void Update()
        {
            if(IsDown)
            {
                if(!_gettingUp)
                {
                    bool fallFinished=IsActiveClipFinished(_rearImpact?FallRearHash:FallHash,FallHash);
                    if(Time.time>=_fallEarliestGetUp&&fallFinished)
                    {
                        if(IsCarTooClose())_safeGetUpAfter=Time.time+extraWaitAfterCarClears;
                        else if(Time.time>=_safeGetUpAfter)StartGettingUp();
                    }
                }
                else
                {
                    bool getUpFinished=IsActiveClipFinished(_rearImpact?GettingUpRearHash:GettingUpHash,GettingUpHash);
                    if(getUpFinished)FinishGettingUp();
                }
                return;
            }

            if(Time.time < _meleeReactionUntil)
            {
                SetRunning(false);SetWalking(false);ApplyGravityOnly();return;
            }

            FindDangerousCar();Vector3 move=Vector3.zero;
            if(_dangerCar!=null)
            {
                Vector3 away=transform.position-_dangerCar.transform.position;away.y=0;if(away.sqrMagnitude<.01f)away=transform.right;away.Normalize();Face(away,turnSpeed*1.45f);move=away*fleeSpeed;SetRunning(true);
            }
            else if(_meleeAttacker!=null && Time.time < _forcedFleeUntil)
            {
                Vector3 away=transform.position-_meleeAttacker.position;away.y=0;if(away.sqrMagnitude<.01f)away=-transform.forward;away.Normalize();Face(away,turnSpeed*1.7f);move=away*punchFleeSpeed;SetRunning(true);
            }
            else if(_pause>0)
            {
                _meleeAttacker=null;SetRunning(false);_pause-=Time.deltaTime;if(_pause<=0)PickTarget();
            }
            else
            {
                _meleeAttacker=null;SetRunning(false);Vector3 to=_target-transform.position;to.y=0;if(to.sqrMagnitude<.16f)Pause(Random.Range(minPause,maxPause));else{Vector3 d=to.normalized;Face(d,turnSpeed);move=d*walkSpeed;SetWalking(true);}
            }
            if(_controller.isGrounded&&_verticalVelocity<0)_verticalVelocity=-2;else _verticalVelocity+=gravity*Time.deltaTime;_controller.Move((move+Vector3.up*_verticalVelocity)*Time.deltaTime);
        }

        private void ApplyGravityOnly(){if(_controller.isGrounded&&_verticalVelocity<0)_verticalVelocity=-2;else _verticalVelocity+=gravity*Time.deltaTime;_controller.Move(Vector3.up*_verticalVelocity*Time.deltaTime);}
        private void Face(Vector3 direction,float speed){if(direction.sqrMagnitude<.001f)return;transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),1f-Mathf.Exp(-speed*Time.deltaTime));}

        private void LateUpdate()
        {
            if(_mainSkinnedMesh==null)return;

            if(IsDown&&!_gettingUp)
            {
                Bounds body=_mainSkinnedMesh.bounds;
                if(!_fallMotionTracking){_fallBodyStartCenter=body.center;_fallMotionTracking=true;}
                else{Vector3 travel=body.center-_fallBodyStartCenter;travel.y=0;if(travel.magnitude>maxAnimationFallTravel)travel=travel.normalized*maxAnimationFallTravel;_impactAnchor.x=_fallOriginAnchor.x+travel.x;_impactAnchor.z=_fallOriginAnchor.z+travel.z;}
            }

            if(Time.time < _meleeReactionUntil)ApplyProceduralHitFallback();
            else if(_visualRoot!=null && _visualRoot.localRotation!=_visualBaseRotation && !IsDown)_visualRoot.localRotation=Quaternion.Slerp(_visualRoot.localRotation,_visualBaseRotation,1f-Mathf.Exp(-16f*Time.deltaTime));

            ResolveBodyGroundContact(IsDown?_impactAnchor:transform.position);
        }

        private void ApplyProceduralHitFallback()
        {
            int hash=_meleeHitVariant==2?Hit2Hash:Hit1Hash;
            if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.HasState(0,hash))return;
            if(_visualRoot==null)return;
            float duration=Mathf.Max(.05f,punchReactionSeconds);
            float t=Mathf.Clamp01((Time.time-_meleeReactionStarted)/duration);
            float kick=Mathf.Sin(t*Mathf.PI);
            float side=_meleeHitVariant==2?-1f:1f;
            _visualRoot.localRotation=_visualBaseRotation*Quaternion.Euler(-18f*kick,side*12f*kick,side*16f*kick);
        }

        public void HitByPlayerPunch(Vector3 hitDirection,int variant,Transform attacker)
        {
            if(IsDown)return;
            _meleeHitVariant=variant==2?2:1;
            _meleeAttacker=attacker;
            _meleeReactionStarted=Time.time;
            _meleeReactionUntil=Time.time+punchReactionSeconds;
            _forcedFleeUntil=_meleeReactionUntil+fleeAfterPunchSeconds+Random.Range(0f,1.5f);
            _dangerCar=null;_walking=false;_running=false;_pause=0f;

            if(hitDirection.sqrMagnitude>.001f)
            {
                hitDirection.y=0f;
                Vector3 face=-hitDirection.normalized;
                if(face.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(face);
            }

            int state=_meleeHitVariant==2?Hit2Hash:Hit1Hash;
            if(animator!=null&&animator.runtimeAnimatorController!=null&&animator.HasState(0,state))animator.CrossFadeInFixedTime(state,.035f,0,0f);
        }

        private void ResolveBodyGroundContact(Vector3 samplePosition){Vector3 ground=FindGroundPoint(samplePosition);Bounds body=_mainSkinnedMesh.bounds;float bottomOffset=body.min.y-transform.position.y;Vector3 p=transform.position;p.y=(ground.y+ForcedBodyGroundClearance)-bottomOffset;if(_gettingUp){p.x=_impactAnchor.x;p.z=_impactAnchor.z;}transform.position=p;if(IsDown)_impactAnchor.y=ground.y;}
        private bool IsActiveClipFinished(int preferredHash,int fallbackHash){if(animator==null)return true;AnimatorStateInfo s=animator.GetCurrentAnimatorStateInfo(0);bool isExpected=s.fullPathHash==preferredHash||s.fullPathHash==fallbackHash;if(!isExpected)return false;return s.normalizedTime>=ClipFinishedNormalizedTime;}
        private bool IsCarTooClose(){foreach(var car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None)){if(car==null)continue;Vector3 d=car.transform.position-_impactAnchor;d.y=0;if(d.sqrMagnitude<=carClearanceBeforeGetUp*carClearanceBeforeGetUp)return true;}return false;}
        private void DisablePhysicalCollision(){if(_allColliders==null)CacheColliders();for(int i=0;i<_allColliders.Length;i++){Collider c=_allColliders[i];if(c==null)continue;_colliderStates[i]=c.enabled;c.enabled=false;}if(_controller!=null)_controller.enabled=false;}
        private void RestorePhysicalCollision(){if(_allColliders!=null)for(int i=0;i<_allColliders.Length;i++)if(_allColliders[i]!=null)_allColliders[i].enabled=_colliderStates[i];if(_controller!=null)_controller.enabled=true;}
        private Vector3 FindGroundPoint(Vector3 desired){Vector3 origin=new Vector3(desired.x,desired.y+groundRayHeight,desired.z);RaycastHit[] hits=Physics.RaycastAll(origin,Vector3.down,groundRayDistance,~0,QueryTriggerInteraction.Ignore);bool found=false;float y=float.NegativeInfinity;foreach(var hit in hits){if(hit.collider==null||hit.transform==transform||hit.transform.IsChildOf(transform)||hit.normal.y<.55f)continue;if(hit.collider.GetComponentInParent<DriveableCar>()!=null||hit.collider.GetComponentInParent<NPCWanderer>()!=null)continue;if(!found||hit.point.y>y){y=hit.point.y;found=true;}}if(found)desired.y=y;return desired;}
        private void StartGettingUp(){_gettingUp=true;_fallMotionTracking=false;int state=_rearImpact?GettingUpRearHash:GettingUpHash;bool ok=PlayState(state,.02f);if(!ok&&_rearImpact)PlayState(GettingUpHash,.02f);}
        private void FinishGettingUp(){ResolveBodyGroundContact(_impactAnchor);_gettingUp=false;_fallEarliestGetUp=0;_fallMotionTracking=false;RestorePhysicalCollision();PlayState(IdleHash,.04f);Pause(Random.Range(.15f,.45f));}
        private bool PlayState(int hash,float fade){if(animator==null||animator.runtimeAnimatorController==null||!animator.isActiveAndEnabled)return false;animator.applyRootMotion=false;if(!animator.HasState(0,hash))return false;animator.CrossFadeInFixedTime(hash,fade,0,0);return true;}
        private void FindDangerousCar(){_dangerCar=null;float best=carAwarenessRadius;foreach(var car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None)){if(car==null||!car.IsOccupied||!car.IsThreateningPoint(transform.position,fleeOnlyAboveKmh))continue;float d=Vector3.Distance(transform.position,car.transform.position);if(d<best){best=d;_dangerCar=car;}}}

        public bool HitByVehicle(Vector3 carVelocity,Vector3 carPosition)
        {
            float speed=carVelocity.magnitude;if(speed<impactSpeedThreshold||IsDown)return false;
            _meleeAttacker=null;_forcedFleeUntil=0f;_meleeReactionUntil=0f;
            Vector3 toCar=carPosition-transform.position;toCar.y=0;if(toCar.sqrMagnitude>.001f)toCar.Normalize();_rearImpact=Vector3.Dot(transform.forward,toCar)<rearHitDotThreshold;
            Vector3 carry=carVelocity;carry.y=0;if(carry.sqrMagnitude>.001f)carry=carry.normalized*impactCarryDistance;
            Vector3 hitPoint=transform.position+carry;_impactAnchor=FindGroundPoint(hitPoint);_impactAnchor.x=hitPoint.x;_impactAnchor.z=hitPoint.z;_fallOriginAnchor=_impactAnchor;
            _fallMotionTracking=false;_fallEarliestGetUp=Time.time+minimumLieSeconds;_safeGetUpAfter=_fallEarliestGetUp;_gettingUp=false;_dangerCar=null;_walking=false;_running=false;
            DisablePhysicalCollision();int state=_rearImpact?FallRearHash:FallHash;bool ok=PlayState(state,0f);if(!ok&&_rearImpact)PlayState(FallHash,0f);return true;
        }

        private void PickTarget(){Vector2 c=Random.insideUnitCircle*wanderRadius;_target=_home+new Vector3(c.x,0,c.y);SetWalking(true);}
        private void Pause(float duration){_pause=duration;SetRunning(false);SetWalking(false);}
        private void SetRunning(bool running){if(_running==running)return;_running=running;if(running){_walking=false;PlayState(RunHash,.08f);}else if(_dangerCar==null&&_walking)PlayState(WalkHash,.08f);}
        private void SetWalking(bool walking){if(_running)return;if(_walking==walking)return;_walking=walking;PlayState(walking?WalkHash:IdleHash,.1f);}
        public void Configure(float speed,float radius){walkSpeed=speed;wanderRadius=radius;}
    }
}
