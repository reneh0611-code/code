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
        [SerializeField] private float fleeOnlyAboveKmh=30f;
        [SerializeField] private float impactSpeedThreshold=.20f;
        [SerializeField] private float lieDownSeconds=1.35f,getUpSeconds=1.35f;
        [SerializeField] private float impactCarryDistance=.20f;
        [SerializeField] private float groundRayHeight=2.5f;
        [SerializeField] private float groundRayDistance=6f;
        [SerializeField] private float downGroundOffset=.015f;
        [SerializeField,Range(-1f,1f)] private float rearHitDotThreshold=-.25f;

        private static readonly int IdleHash=Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkHash=Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunHash=Animator.StringToHash("Base Layer.Run");
        private static readonly int FallHash=Animator.StringToHash("Base Layer.Fall");
        private static readonly int GettingUpHash=Animator.StringToHash("Base Layer.GettingUp");
        private static readonly int FallRearHash=Animator.StringToHash("Base Layer.FallRear");
        private static readonly int GettingUpRearHash=Animator.StringToHash("Base Layer.GettingUpRear");

        private CharacterController _controller;
        private Vector3 _home,_target,_impactAnchor;
        private float _pause,_verticalVelocity,_fallUntil,_getUpUntil;
        private bool _walking,_running,_gettingUp,_rearImpact;
        private DriveableCar _dangerCar;

        private Transform _visualRoot;
        private Vector3 _visualBaseLocalPosition;
        private SkinnedMeshRenderer _mainSkinnedMesh;
        private bool _visualCached;

        public bool IsDown=>_fallUntil>0f||_gettingUp;
        public Vector3 DownPosition=>_impactAnchor;

        private void Awake()
        {
            _controller=GetComponent<CharacterController>();
            if(animator==null)animator=GetComponentInChildren<Animator>(true);
            CacheVisual();
        }

        private void CacheVisual()
        {
            if(animator==null)return;
            animator.applyRootMotion=false;
            _visualRoot=animator.transform;
            _visualBaseLocalPosition=_visualRoot.localPosition;

            SkinnedMeshRenderer[] skins=animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            float bestVolume=-1f;
            foreach(SkinnedMeshRenderer skin in skins)
            {
                if(skin==null)continue;
                Vector3 s=skin.bounds.size;
                float volume=Mathf.Abs(s.x*s.y*s.z);
                if(volume>bestVolume){bestVolume=volume;_mainSkinnedMesh=skin;}
            }

            _visualCached=_visualRoot!=null&&_mainSkinnedMesh!=null;
            if(_visualCached)
                Debug.Log($"[CYDOY] NPC visual anchor: {name} mainMesh={_mainSkinnedMesh.name}",this);
        }

        private void Start(){_home=transform.position;Pause(Random.Range(.4f,2.5f));}

        private void Update()
        {
            if(Time.time<_fallUntil){HoldImpactRoot();return;}
            if(!_gettingUp&&_fallUntil>0f&&Time.time>=_fallUntil)StartGettingUp();

            if(_gettingUp)
            {
                HoldImpactRoot();
                if(Time.time>=_getUpUntil)
                {
                    _gettingUp=false;
                    _fallUntil=0f;
                    if(_controller!=null)_controller.detectCollisions=true;
                    RestoreVisualRoot();
                    PlayState(IdleHash,.08f);
                    Pause(Random.Range(.15f,.45f));
                }
                return;
            }

            FindDangerousCar();
            Vector3 horizontal=Vector3.zero;

            if(_dangerCar!=null)
            {
                Vector3 away=transform.position-_dangerCar.transform.position;
                away.y=0f;
                if(away.sqrMagnitude<.01f)away=transform.right;
                away.Normalize();
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(away),1f-Mathf.Exp(-turnSpeed*1.45f*Time.deltaTime));
                horizontal=away*fleeSpeed;
                SetRunning(true);
            }
            else if(_pause>0f)
            {
                SetRunning(false);
                _pause-=Time.deltaTime;
                if(_pause<=0f)PickTarget();
            }
            else
            {
                SetRunning(false);
                Vector3 to=_target-transform.position;
                to.y=0f;
                if(to.sqrMagnitude<.16f)Pause(Random.Range(minPause,maxPause));
                else
                {
                    Vector3 d=to.normalized;
                    transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(d),1f-Mathf.Exp(-turnSpeed*Time.deltaTime));
                    horizontal=d*walkSpeed;
                    SetWalking(true);
                }
            }

            if(_controller.isGrounded&&_verticalVelocity<0f)_verticalVelocity=-2f;
            else _verticalVelocity+=gravity*Time.deltaTime;
            _controller.Move((horizontal+Vector3.up*_verticalVelocity)*Time.deltaTime);
        }

        private void LateUpdate()
        {
            if(!IsDown)return;
            AnchorAnimatedBodyToRoad();
        }

        private void HoldImpactRoot()
        {
            _impactAnchor=FindGroundPoint(_impactAnchor);
            if(_controller!=null&&_controller.enabled)
            {
                Vector3 delta=_impactAnchor-transform.position;
                if(delta.sqrMagnitude>.000001f)_controller.Move(delta);
            }
            else transform.position=_impactAnchor;
            _verticalVelocity=0f;
        }

        private void AnchorAnimatedBodyToRoad()
        {
            if(!_visualCached)CacheVisual();
            if(!_visualCached||_visualRoot==transform)return;

            Vector3 ground=FindGroundPoint(_impactAnchor);

            // First neutralize translation authored on the imported model root.
            Vector3 lp=_visualRoot.localPosition;
            lp.x=_visualBaseLocalPosition.x;
            lp.z=_visualBaseLocalPosition.z;
            _visualRoot.localPosition=lp;

            // Then compensate what the actual animated skeleton did. Using the largest skinned mesh
            // avoids accessories or stray renderers fooling the grounding calculation.
            Bounds body=_mainSkinnedMesh.bounds;
            Vector3 bodyCenter=body.center;

            Vector3 horizontalCorrection=new Vector3(
                ground.x-bodyCenter.x,
                0f,
                ground.z-bodyCenter.z);
            _visualRoot.position+=horizontalCorrection;

            // Bounds changed after the horizontal move; read them again before vertical grounding.
            body=_mainSkinnedMesh.bounds;
            float targetBottom=ground.y+downGroundOffset;
            float verticalCorrection=targetBottom-body.min.y;
            _visualRoot.position+=Vector3.up*verticalCorrection;
        }

        private void RestoreVisualRoot()
        {
            if(!_visualCached)CacheVisual();
            if(_visualRoot!=null&&_visualRoot!=transform)_visualRoot.localPosition=_visualBaseLocalPosition;
        }

        private Vector3 FindGroundPoint(Vector3 desired)
        {
            Vector3 origin=desired+Vector3.up*groundRayHeight;
            RaycastHit[] hits=Physics.RaycastAll(origin,Vector3.down,groundRayDistance,~0,QueryTriggerInteraction.Ignore);
            bool found=false;
            float bestY=float.NegativeInfinity;

            foreach(RaycastHit hit in hits)
            {
                if(hit.collider==null||hit.transform==transform||hit.transform.IsChildOf(transform)||hit.normal.y<.55f)continue;
                if(hit.collider.GetComponentInParent<DriveableCar>()!=null)continue;
                if(!found||hit.point.y>bestY){bestY=hit.point.y;found=true;}
            }

            if(found)desired.y=bestY+downGroundOffset;
            return desired;
        }

        private void StartGettingUp()
        {
            _gettingUp=true;
            _getUpUntil=Time.time+getUpSeconds;
            int state=_rearImpact?GettingUpRearHash:GettingUpHash;
            bool played=PlayState(state,.04f);
            if(!played&&_rearImpact)played=PlayState(GettingUpHash,.04f);
            Debug.Log($"[CYDOY] NPC GETTING UP: {name} rear={_rearImpact} animation={played}",this);
        }

        private bool PlayState(int hash,float fade)
        {
            if(animator==null)animator=GetComponentInChildren<Animator>(true);
            if(animator==null||animator.runtimeAnimatorController==null||!animator.isActiveAndEnabled)return false;
            animator.applyRootMotion=false;
            if(!animator.HasState(0,hash))return false;
            animator.CrossFadeInFixedTime(hash,fade,0,0f);
            return true;
        }

        private void FindDangerousCar()
        {
            _dangerCar=null;
            float best=carAwarenessRadius;
            foreach(DriveableCar car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None))
            {
                if(car==null||!car.IsOccupied)continue;
                if(!car.IsThreateningPoint(transform.position,fleeOnlyAboveKmh))continue;
                float d=Vector3.Distance(transform.position,car.transform.position);
                if(d<best){best=d;_dangerCar=car;}
            }
        }

        public bool HitByVehicle(Vector3 carVelocity,Vector3 carPosition)
        {
            float speed=carVelocity.magnitude;
            if(speed<impactSpeedThreshold||Time.time<_fallUntil||_gettingUp)return false;

            Vector3 toCar=carPosition-transform.position;
            toCar.y=0f;
            if(toCar.sqrMagnitude>.001f)toCar.Normalize();
            float dot=Vector3.Dot(transform.forward,toCar);
            _rearImpact=dot<rearHitDotThreshold;

            Vector3 carry=carVelocity;
            carry.y=0f;
            if(carry.sqrMagnitude>.001f)carry=carry.normalized*impactCarryDistance;
            _impactAnchor=FindGroundPoint(transform.position+carry);

            _fallUntil=Time.time+lieDownSeconds;
            _getUpUntil=0f;
            _gettingUp=false;
            _dangerCar=null;
            _walking=false;
            _running=false;

            if(_controller!=null)_controller.detectCollisions=false;
            if(animator!=null)animator.applyRootMotion=false;

            int state=_rearImpact?FallRearHash:FallHash;
            bool played=PlayState(state,0f);
            if(!played&&_rearImpact)played=PlayState(FallHash,0f);

            Debug.Log($"[CYDOY] NPC HIT: {name} speed={speed:F2} rear={_rearImpact} animation={played} ground={_impactAnchor.y:F3}",this);
            return true;
        }

        private void PickTarget(){Vector2 c=Random.insideUnitCircle*wanderRadius;_target=_home+new Vector3(c.x,0f,c.y);SetWalking(true);}
        private void Pause(float duration){_pause=duration;SetRunning(false);SetWalking(false);}
        private void SetRunning(bool running){if(_running==running)return;_running=running;if(running){_walking=false;PlayState(RunHash,.08f);}else if(_dangerCar==null&&_walking)PlayState(WalkHash,.08f);}
        private void SetWalking(bool walking){if(_running)return;if(_walking==walking)return;_walking=walking;PlayState(walking?WalkHash:IdleHash,.1f);}
        public void Configure(float speed,float radius){walkSpeed=speed;wanderRadius=radius;}
    }
}
