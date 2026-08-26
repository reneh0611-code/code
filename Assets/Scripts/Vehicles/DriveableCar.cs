using CheatOnYourDayOnes.CameraSystem;
using CheatOnYourDayOnes.World;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DriveableCar : MonoBehaviour
    {
        [SerializeField] private Transform driverSeat,exitPoint,centerOfMass;
        [Header("Map-scale vehicle handling")]
        [SerializeField] private float topSpeed=10.5f,reverseTopSpeed=3.8f,forwardAcceleration=1.35f,reverseAcceleration=1.25f,engineBraking=.75f,brakeAcceleration=7.5f;
        [SerializeField] private float steeringRate=48f,highSpeedSteerFactor=.34f,lateralGripLowSpeed=7.5f,lateralGripHighSpeed=2.3f,throttleResponse=1.35f,steeringResponse=4.2f;
        [SerializeField] private float steeringYawAcceleration=95f;
        [SerializeField] private float interactionDistance=3.5f,tyreGroundClearance=.015f,rollingResistance=.18f;

        [Header("NPC impact detection")]
        [SerializeField] private float frontImpactExtraDepth=.28f;
        [SerializeField] private float frontImpactWidthFactor=.48f;
        [SerializeField] private float overrunRollKick=.18f;
        [SerializeField] private float overrunVerticalKick=.035f;

        [Header("NPC impact response")]
        [SerializeField] private float fullStopBelowKmh=20f,heavyBrakeBelowKmh=30f;
        [SerializeField,Range(0,1)] private float mediumImpactSpeedRetention=.45f;

        private Rigidbody _rb;private Transform _driver;private CharacterController _driverController;private Behaviour _networkController;private VehicleInteractor _interactor;private Renderer[] _driverRenderers;private bool[] _driverRendererStates;private Collider[] _driverColliders;private bool[] _driverColliderStates;private ThirdPersonCamera _camera;private BoxCollider _chassisCollider;
        private readonly List<Renderer> _wheelRenderers=new();private readonly List<SphereCollider> _wheelSupportColliders=new();private readonly HashSet<NPCWanderer> _npcHitThisContact=new();private readonly HashSet<NPCWanderer> _npcOverrunContact=new();
        private bool _occupied,_ignoreExitUntilEReleased,_brake;private float _rawThrottle,_rawSteer,_throttle,_steer,_driveSpeed,_debugTimer,_modelScale=1f,_yawRate;

        public bool IsOccupied=>_occupied;
        public Vector3 DriveVelocity=>transform.forward*_driveSpeed;
        public float SpeedKmh=>Mathf.Abs(_driveSpeed)*3.6f;

        public bool IsThreateningPoint(Vector3 worldPoint,float minimumKmh=30f)
        {
            if(!_occupied||SpeedKmh<minimumKmh||Mathf.Abs(_driveSpeed)<.01f)return false;
            Vector3 toPoint=worldPoint-transform.position;toPoint.y=0;
            if(toPoint.sqrMagnitude<.001f)return true;
            toPoint.Normalize();
            Vector3 travelDirection=_driveSpeed>=0?transform.forward:-transform.forward;
            return Vector3.Dot(travelDirection,toPoint)>.35f;
        }

        private void Awake(){_rb=GetComponent<Rigidbody>();DetectWheelsAndScale();ConfigureRigidbody();RebuildVehicleColliders();}

        private void ConfigureRigidbody()
        {
            _rb.mass=1350;_rb.useGravity=true;_rb.isKinematic=false;_rb.constraints=RigidbodyConstraints.None;
            _rb.linearDamping=.03f;_rb.angularDamping=2.6f;_rb.interpolation=RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;_rb.maxAngularVelocity=3;
            _rb.centerOfMass=centerOfMass!=null?transform.InverseTransformPoint(centerOfMass.position):new Vector3(0,-.42f*_modelScale,.08f*_modelScale);
        }

        private void DetectWheelsAndScale()
        {
            _wheelRenderers.Clear();Renderer[] rs=GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return;
            Bounds all=rs[0].bounds;
            foreach(Renderer r in rs)
            {
                if(r==null)continue;all.Encapsulate(r.bounds);
                string n=r.name.ToLowerInvariant(),p=r.transform.parent!=null?r.transform.parent.name.ToLowerInvariant():"";
                if(LooksLikeWheelName(n)||LooksLikeWheelName(p))_wheelRenderers.Add(r);
            }
            float length=Mathf.Max(all.size.x,all.size.z);_modelScale=Mathf.Clamp(length/4.5f,.5f,3);
        }

        private static bool LooksLikeWheelName(string n)=>n.Contains("wheel")||n.Contains("tire")||n.Contains("tyre")||n.Contains("reifen")||n.Contains("felge")||n.Contains("rim")||n.Contains("roue")||n.Contains("rad_");

        private void Update()
        {
            if(!_occupied||Keyboard.current==null)return;
            _rawThrottle=(Keyboard.current.wKey.isPressed?1f:0f)-(Keyboard.current.sKey.isPressed?1f:0f);
            _rawSteer=(Keyboard.current.dKey.isPressed?1f:0f)-(Keyboard.current.aKey.isPressed?1f:0f);
            _brake=Keyboard.current.spaceKey.isPressed;
            _throttle=Mathf.MoveTowards(_throttle,_rawThrottle,throttleResponse*Time.deltaTime);
            _steer=Mathf.MoveTowards(_steer,_rawSteer,steeringResponse*Time.deltaTime);
            if(_ignoreExitUntilEReleased){if(!Keyboard.current.eKey.isPressed)_ignoreExitUntilEReleased=false;}
            else if(Keyboard.current.eKey.wasPressedThisFrame)Exit();
        }

        private void FixedUpdate()
        {
            if(!_occupied)return;
            if(_brake)_driveSpeed=Mathf.MoveTowards(_driveSpeed,0f,brakeAcceleration*Time.fixedDeltaTime);
            else if(_throttle>.01f)_driveSpeed=Mathf.MoveTowards(_driveSpeed,topSpeed,forwardAcceleration*Mathf.Max(.18f,_throttle)*Time.fixedDeltaTime);
            else if(_throttle<-.01f)_driveSpeed=Mathf.MoveTowards(_driveSpeed,-reverseTopSpeed,reverseAcceleration*Mathf.Max(.18f,-_throttle)*Time.fixedDeltaTime);
            else _driveSpeed=Mathf.MoveTowards(_driveSpeed,0f,(engineBraking+rollingResistance*Mathf.Abs(_driveSpeed))*Time.fixedDeltaTime);

            Vector3 local=transform.InverseTransformDirection(_rb.linearVelocity);
            float speed01=Mathf.Clamp01(Mathf.Abs(_driveSpeed)/topSpeed),grip=Mathf.Lerp(lateralGripLowSpeed,lateralGripHighSpeed,speed01);
            local.x=Mathf.MoveTowards(local.x,0f,grip*Time.fixedDeltaTime);local.z=_driveSpeed;
            Vector3 wanted=transform.TransformDirection(new Vector3(local.x,0f,local.z));wanted.y=_rb.linearVelocity.y;_rb.linearVelocity=wanted;

            float authority=Mathf.Clamp01(Mathf.Abs(_driveSpeed)/1.7f);
            float steerMult=Mathf.Lerp(1f,highSpeedSteerFactor,speed01);
            float reverse=_driveSpeed<-.05f?-1f:1f;
            float targetYawRateDeg=_steer*steeringRate*steerMult*authority*reverse;
            _yawRate=Mathf.MoveTowards(_yawRate,targetYawRateDeg,steeringYawAcceleration*Time.fixedDeltaTime);
            if(Mathf.Abs(_steer)<.01f)_yawRate=Mathf.MoveTowards(_yawRate,0f,steeringYawAcceleration*1.35f*Time.fixedDeltaTime);

            Vector3 av=_rb.angularVelocity;
            av.y=_yawRate*Mathf.Deg2Rad;
            av.x*=.82f;av.z*=.82f;
            _rb.angularVelocity=av;

            DetectNPCHits();
            DetectNPCOverrun();

            _debugTimer+=Time.fixedDeltaTime;
            if(_debugTimer>=1f&&Mathf.Abs(_rawThrottle)>.1f)
            {
                _debugTimer=0;
                Debug.Log($"[CYDOY] CAR INPUT={_rawThrottle:F0} throttle={_throttle:F2} commanded={_driveSpeed:F2}m/s actual={transform.InverseTransformDirection(_rb.linearVelocity).z:F2}m/s yawRate={_yawRate:F1}",this);
            }
        }

        private void DetectNPCHits()
        {
            if(_chassisCollider==null)return;

            Vector3 worldScale=AbsVector(transform.lossyScale);
            float chassisWidth=_chassisCollider.size.x*worldScale.x;
            float chassisHeight=_chassisCollider.size.y*worldScale.y;
            float chassisLength=_chassisCollider.size.z*worldScale.z;

            Vector3 localFront=_chassisCollider.center+Vector3.forward*(_chassisCollider.size.z*.5f+frontImpactExtraDepth*.5f);
            Vector3 probeCenter=transform.TransformPoint(localFront);
            Vector3 half=new Vector3(
                chassisWidth*frontImpactWidthFactor,
                Mathf.Max(.45f,chassisHeight*.72f),
                Mathf.Max(.18f,frontImpactExtraDepth*.5f*_modelScale));

            Collider[] hits=Physics.OverlapBox(probeCenter,half,transform.rotation,~0,QueryTriggerInteraction.Collide);
            HashSet<NPCWanderer> current=new();

            foreach(Collider hit in hits)
            {
                if(hit==null||hit.transform.IsChildOf(transform))continue;
                NPCWanderer npc=hit.GetComponentInParent<NPCWanderer>();
                if(npc==null||npc.IsDown)continue;

                Vector3 npcLocal=transform.InverseTransformPoint(npc.transform.position);
                float maxSide=(_chassisCollider.size.x*.5f)*.93f;
                if(Mathf.Abs(npcLocal.x-_chassisCollider.center.x)>maxSide)continue;

                current.Add(npc);
                if(_npcHitThisContact.Contains(npc))continue;

                float kmh=SpeedKmh;
                if(npc.HitByVehicle(DriveVelocity,transform.position))
                {
                    ApplyNPCImpactSpeedResponse(kmh);
                    _npcHitThisContact.Add(npc);
                }
            }

            _npcHitThisContact.RemoveWhere(n=>n==null||!current.Contains(n));
        }

        private void DetectNPCOverrun()
        {
            if(_chassisCollider==null||Mathf.Abs(_driveSpeed)<1f)return;

            HashSet<NPCWanderer> current=new();
            float halfWidth=_chassisCollider.size.x*.5f*.92f;
            float halfLength=_chassisCollider.size.z*.5f*1.05f;

            foreach(NPCWanderer npc in Object.FindObjectsByType<NPCWanderer>(FindObjectsSortMode.None))
            {
                if(npc==null||!npc.IsDown)continue;

                Vector3 local=transform.InverseTransformPoint(npc.DownPosition);
                bool underneath=Mathf.Abs(local.x-_chassisCollider.center.x)<=halfWidth &&
                                 Mathf.Abs(local.z-_chassisCollider.center.z)<=halfLength &&
                                 Mathf.Abs(local.y-_chassisCollider.center.y)<2f*_modelScale;
                if(!underneath)continue;

                current.Add(npc);
                if(_npcOverrunContact.Contains(npc))continue;

                float side=Mathf.Sign(local.x-_chassisCollider.center.x);
                if(Mathf.Abs(side)<.01f)side=Random.value>.5f?1f:-1f;

                // Simulated suspension/body roll only. Fallen NPC collision is disabled, so the car
                // cannot climb the capsule or launch into the air.
                Vector3 angular=_rb.angularVelocity;
                angular+=transform.forward*(side*overrunRollKick);
                angular.x=Mathf.Clamp(angular.x,-.5f,.5f);
                angular.z=Mathf.Clamp(angular.z,-.5f,.5f);
                _rb.angularVelocity=angular;

                Vector3 velocity=_rb.linearVelocity;
                velocity.y=Mathf.Min(velocity.y+overrunVerticalKick,.12f);
                _rb.linearVelocity=velocity;

                _npcOverrunContact.Add(npc);
                Debug.Log($"[CYDOY] NPC OVERRUN: {npc.name} side={(side<0?"left":"right")} rollKick={overrunRollKick:F2}",this);
            }

            _npcOverrunContact.RemoveWhere(n=>n==null||!current.Contains(n));
        }

        private void ApplyNPCImpactSpeedResponse(float kmh)
        {
            if(kmh<=fullStopBelowKmh)
            {
                _driveSpeed=0;_yawRate=0;Vector3 v=_rb.linearVelocity;v.x=0;v.z=0;_rb.linearVelocity=v;
            }
            else if(kmh<heavyBrakeBelowKmh)_driveSpeed*=mediumImpactSpeedRetention;
        }

        public float DistanceFrom(Vector3 p)
        {
            float best=float.MaxValue;
            foreach(Collider c in GetComponentsInChildren<Collider>(true))
            {
                if(c==null||!c.enabled||c.isTrigger)continue;
                best=Mathf.Min(best,Vector3.Distance(p,c.ClosestPoint(p)));
            }
            return best<float.MaxValue?best:Vector3.Distance(p,transform.position);
        }

        public bool TryEnter(Transform player)
        {
            if(_occupied||player==null||DistanceFrom(player.position)>interactionDistance)return false;
            _driver=player;_driverController=player.GetComponent<CharacterController>();
            _networkController=player.GetComponent<CheatOnYourDayOnes.Player.NetworkPlayerController>();
            _interactor=player.GetComponent<VehicleInteractor>();
            if(_networkController!=null)_networkController.enabled=false;if(_interactor!=null)_interactor.enabled=false;

            _driverRenderers=player.GetComponentsInChildren<Renderer>(true);_driverRendererStates=new bool[_driverRenderers.Length];
            for(int i=0;i<_driverRenderers.Length;i++){_driverRendererStates[i]=_driverRenderers[i].enabled;_driverRenderers[i].enabled=false;}
            _driverColliders=player.GetComponentsInChildren<Collider>(true);_driverColliderStates=new bool[_driverColliders.Length];
            for(int i=0;i<_driverColliders.Length;i++){_driverColliderStates[i]=_driverColliders[i].enabled;_driverColliders[i].enabled=false;}
            if(_driverController!=null)_driverController.enabled=false;

            Transform seat=driverSeat!=null?driverSeat:transform;player.SetParent(seat,false);player.localPosition=Vector3.zero;player.localRotation=Quaternion.identity;
            _camera=Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);if(_camera!=null)_camera.EnterVehicleMode(transform);

            DetectWheelsAndScale();ConfigureRigidbody();RebuildVehicleColliders();PutTyresOnGround();
            _driveSpeed=_throttle=_steer=_yawRate=0;_rb.linearVelocity=Vector3.zero;_rb.angularVelocity=Vector3.zero;_rb.WakeUp();
            _occupied=true;_ignoreExitUntilEReleased=true;_npcHitThisContact.Clear();_npcOverrunContact.Clear();
            Debug.Log("[CYDOY] VEHICLE READY - tight NPC hitbox + soft overrun active",this);
            return true;
        }

        public void Exit()
        {
            if(!_occupied||_driver==null)return;
            Transform p=_driver;p.SetParent(null,true);p.position=exitPoint!=null?exitPoint.position:transform.position-transform.right*1.8f+Vector3.up*.25f;p.rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);
            if(_camera!=null)_camera.ExitVehicleMode(p);
            if(_driverRenderers!=null)for(int i=0;i<_driverRenderers.Length;i++)if(_driverRenderers[i]!=null)_driverRenderers[i].enabled=_driverRendererStates[i];
            if(_driverColliders!=null)for(int i=0;i<_driverColliders.Length;i++)if(_driverColliders[i]!=null)_driverColliders[i].enabled=_driverColliderStates[i];
            if(_driverController!=null)_driverController.enabled=true;if(_networkController!=null)_networkController.enabled=true;if(_interactor!=null)_interactor.enabled=true;
            _driver=null;_occupied=false;_rawThrottle=_rawSteer=_throttle=_steer=_driveSpeed=_yawRate=0;_brake=false;_npcHitThisContact.Clear();_npcOverrunContact.Clear();
        }

        private void RebuildVehicleColliders()
        {
            foreach(Collider c in GetComponentsInChildren<Collider>(true)){if(c==null||c==_chassisCollider||_wheelSupportColliders.Contains(c as SphereCollider)||c.isTrigger)continue;c.enabled=false;}
            foreach(SphereCollider old in _wheelSupportColliders)if(old!=null)Destroy(old.gameObject);_wheelSupportColliders.Clear();
            Renderer[] rs=GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return;Bounds all=rs[0].bounds;foreach(Renderer r in rs)if(r!=null)all.Encapsulate(r.bounds);
            Vector3 min=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=new(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
            foreach(Vector3 corner in BoundsCorners(all)){Vector3 l=transform.InverseTransformPoint(corner);min=Vector3.Min(min,l);max=Vector3.Max(max,l);}
            if(_chassisCollider==null){_chassisCollider=GetComponent<BoxCollider>();if(_chassisCollider==null)_chassisCollider=gameObject.AddComponent<BoxCollider>();}
            Vector3 raw=max-min;_chassisCollider.size=new Vector3(raw.x*.82f,raw.y*.32f,raw.z*.84f);_chassisCollider.center=new Vector3((min.x+max.x)*.5f,min.y+raw.y*.66f,(min.z+max.z)*.5f);_chassisCollider.enabled=true;_chassisCollider.isTrigger=false;
            foreach(Renderer wheel in _wheelRenderers)
            {
                if(wheel==null||!wheel.enabled)continue;Bounds wb=wheel.bounds;float radiusWorld=Mathf.Clamp(wb.size.y*.47f,.16f*_modelScale,.48f*_modelScale);
                GameObject support=new("CYDOY_WheelSupport_"+wheel.name);support.transform.SetParent(transform,false);support.transform.position=wb.center;
                SphereCollider sphere=support.AddComponent<SphereCollider>();float ps=Mathf.Max(.0001f,Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Max(Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z))));sphere.radius=radiusWorld/ps;_wheelSupportColliders.Add(sphere);
            }
        }

        private void PutTyresOnGround()
        {
            if(_wheelRenderers.Count==0)return;float bottom=float.PositiveInfinity;Vector3 avg=Vector3.zero;int count=0;
            foreach(Renderer wheel in _wheelRenderers){if(wheel==null||!wheel.enabled)continue;bottom=Mathf.Min(bottom,wheel.bounds.min.y);avg+=wheel.bounds.center;count++;}
            if(count==0)return;avg/=count;
            RaycastHit[] hits=Physics.RaycastAll(new Vector3(avg.x,avg.y+3*_modelScale,avg.z),Vector3.down,10*_modelScale,~0,QueryTriggerInteraction.Ignore);
            bool found=false;float ground=float.NegativeInfinity;
            foreach(RaycastHit hit in hits){if(hit.collider==null||hit.transform==transform||hit.transform.IsChildOf(transform)||hit.normal.y<.65f)continue;if(!found||hit.point.y>ground){ground=hit.point.y;found=true;}}
            if(!found)return;transform.position+=Vector3.up*(ground+tyreGroundClearance-bottom);Physics.SyncTransforms();
        }

        private static Vector3 AbsVector(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        private static Vector3[] BoundsCorners(Bounds b){Vector3 min=b.min,max=b.max;return new[]{new Vector3(min.x,min.y,min.z),new Vector3(max.x,min.y,min.z),new Vector3(min.x,max.y,min.z),new Vector3(max.x,max.y,min.z),new Vector3(min.x,min.y,max.z),new Vector3(max.x,min.y,max.z),new Vector3(min.x,max.y,max.z),new Vector3(max.x,max.y,max.z)};}
    }
}
