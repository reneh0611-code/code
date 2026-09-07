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
        private static readonly HashSet<DriveableCar> ActiveCarSet = new();
        private static PhysicsMaterial s_tyreMaterial,s_chassisMaterial;
        public static IEnumerable<DriveableCar> ActiveCars => ActiveCarSet;

        [SerializeField] private Transform driverSeat,exitPoint,centerOfMass;
        [Header("Map-scale vehicle handling")]
        [SerializeField] private float topSpeed=16.5f,reverseTopSpeed=5.2f,forwardAcceleration=2.7f,reverseAcceleration=2.05f,engineBraking=.72f,brakeAcceleration=10.8f;
        [SerializeField] private float highSpeedSteerFactor=.42f,lateralGripLowSpeed=9.2f,lateralGripHighSpeed=3.6f,throttleResponse=2.25f,steeringResponse=1.65f;
        [SerializeField] private float steeringReturnMultiplier=2.25f,highSpeedSteeringBuildMultiplier=.78f;
        [SerializeField] private float maximumRoadWheelAngle=34f,wheelBase=2.35f,maximumLateralAcceleration=7.2f,steeringYawAcceleration=165f;
        [SerializeField] private float smallBumpStability=18f,smallBumpHeightLimit=.11f;
        [Header("Wheel suspension")]
        [SerializeField] private float wheelSuspensionDistance=.18f,wheelSpring=33000f,wheelDamper=5200f,suspensionTargetPosition=.52f;
        [SerializeField] private float maximumMotorTorque=310f,serviceBrakeTorque=1650f,directionChangeBrakeTorque=900f,parkBrakeTorque=260f,antiRollForce=5200f;
        [SerializeField] private float wheelVisualGroundInset=.018f;
        [SerializeField] private float aerodynamicDownforce=1.5f;
        [SerializeField] private float sidewaysTyreStiffness=1.8f,tractionControlSlip=.5f,tractionTorqueFloor=.5f;
        [Header("Crash damage")]
        [SerializeField] private float minimumDamageImpactKmh=9f,crashResistance=1f;
        [SerializeField] private float rolloverThresholdKmh=115f,rolloverSteerThreshold=.62f,rolloverStrength=6f,impactTripStrength=.42f;
        [SerializeField] private float bodyHealth=100f,engineHealth=100f;
        [SerializeField] private float frontBodyHealth=100f,rearBodyHealth=100f,leftBodyHealth=100f,rightBodyHealth=100f;
        [Header("Road seam filter")]
        [SerializeField] private float curbReferenceHeight=.17f;
        [SerializeField,Range(.1f,.9f)] private float suspensionActivationPercent=.42f;
        [SerializeField] private float blockingObstacleHeight=.42f,bumperProbeDistance=.46f;
        [SerializeField] private float interactionDistance=3.5f,tyreGroundClearance=0f,rollingResistance=.18f;
        [Header("NPC impact detection")]
        [SerializeField] private float bumperReach=.72f;
        [SerializeField] private float bumperSideMargin=.12f;
        [SerializeField] private float overrunRollKick=.18f;
        [SerializeField] private float overrunVerticalKick=.02f;
        [Header("NPC impact response")]
        [SerializeField] private float fullStopBelowKmh=20f,heavyBrakeBelowKmh=30f;
        [SerializeField,Range(0,1)] private float mediumImpactSpeedRetention=.45f;
        private Rigidbody _rb;private Transform _driver;private CharacterController _driverController;private Behaviour _networkController;private VehicleInteractor _interactor;private Renderer[] _driverRenderers;private bool[] _driverRendererStates;private Collider[] _driverColliders;private bool[] _driverColliderStates;private ThirdPersonCamera _camera;private BoxCollider _chassisCollider;
        private sealed class WheelPhysics
        {
            public WheelCollider collider;
            public Transform steeringPivot,spinPivot;
            public Quaternion steeringBaseRotation,spinBaseRotation;
            public bool front,left;
            public float spinDegrees,health=100f;
        }
        private sealed class BodyMaterialState{public Renderer renderer;public Material material;public Color originalColor;public int colorProperty;public float localDamage;}
        private readonly List<Renderer> _wheelRenderers=new();private readonly List<SphereCollider> _wheelSupportColliders=new();private readonly List<WheelPhysics> _physicsWheels=new();private readonly HashSet<NPCWanderer> _npcHitThisContact=new();private readonly HashSet<NPCWanderer> _npcOverrunContact=new();private readonly HashSet<NPCWanderer> _npcContactScratch=new();private readonly HashSet<NPCWanderer> _npcOverrunScratch=new();
        private VehicleBodyDamage _bodyDamage;
        private float _centerHeight=.58f,_rearDriveShare=.55f;
        private float _steeringSafetyMargin=1.55f,_driftBlend;
        private bool _handbrake;
        public int DamageRevision=>_bodyDamage!=null?_bodyDamage.Revision:0;
        public bool TryGetWheelPosition(bool front,bool left,out Vector3 local)
        {
            foreach(WheelPhysics wheel in _physicsWheels)if(wheel.front==front&&wheel.left==left&&wheel.collider!=null){local=transform.InverseTransformPoint(wheel.collider.transform.position);return true;}
            local=Vector3.zero;return false;
        }
        private float _bodyContactUntil;
        private Vector3 _bodyContactNormal=Vector3.up;
        private readonly List<BodyMaterialState> _bodyMaterialStates=new();private readonly List<GameObject> _impactScars=new();private Material _impactScarMaterial;
        private bool _occupied,_ignoreExitUntilEReleased,_brake,_smoothRoadContact,_isDrifting,_vehicleDisabled,_rolloverActive;private float _rawThrottle,_rawSteer,_throttle,_steer,_driveSpeed,_debugTimer,_modelScale=1f,_yawRate,_visualSteeringInput,_wheelContactLocalY,_vehicleMass=1280f,_engineRpm=900f,_rearGripMultiplier=1f,_lastCrashTime=-10f,_collisionRecoveryUntil,_centerOfMassYOffset=-.42f,_rolloverRisk,_rolloverBuild,_rolloverEnergy,_rolloverDirection,_rolloverImpactCooldown,_postRolloverRestTime,_rolloverElapsed;private int _currentGear=1,_rolloverGroundHits;private Vector3 _rolloverAxis;private string _vehicleLabel="Auto";private Collider _lastCrashCollider;private ParticleSystem _crashParticles,_damageSmoke;
        public bool IsOccupied=>_occupied;public Vector3 DriveVelocity=>_rb!=null?_rb.linearVelocity:transform.forward*_driveSpeed;public float SpeedKmh=>_rb!=null?_rb.linearVelocity.magnitude*3.6f:0f;
        public string VehicleLabel=>_vehicleLabel;
        public float EngineRpm=>_engineRpm;
        public float EngineRpm01=>Mathf.InverseLerp(900f,7200f,_engineRpm);
        public float TopSpeedKmh=>topSpeed*3.6f;
        public int CurrentGear=>_currentGear;
        public bool IsDrifting=>_isDrifting;
        public float BodyHealth=>bodyHealth;
        public float EngineHealth=>engineHealth;
        public float FrontBodyHealth=>frontBodyHealth;
        public float RearBodyHealth=>rearBodyHealth;
        public float LeftBodyHealth=>leftBodyHealth;
        public float RightBodyHealth=>rightBodyHealth;
        public float FrontLeftWheelHealth=>GetWheelHealth(true,true);
        public float FrontRightWheelHealth=>GetWheelHealth(true,false);
        public float RearLeftWheelHealth=>GetWheelHealth(false,true);
        public float RearRightWheelHealth=>GetWheelHealth(false,false);
        public float AverageWheelHealth=>(FrontLeftWheelHealth+FrontRightWheelHealth+RearLeftWheelHealth+RearRightWheelHealth)*.25f;
        public float VehicleCondition01=>Mathf.Clamp01((bodyHealth*.42f+engineHealth*.23f+AverageWheelHealth*.15f+(frontBodyHealth+rearBodyHealth+leftBodyHealth+rightBodyHealth)*.05f)/100f);
        public bool IsVehicleDisabled=>_vehicleDisabled;
        public float SignedDriveSpeed=>_driveSpeed;
        public float SteeringInput=>_steer;
        public float VisualSteeringInput=>_visualSteeringInput;
        public float SignedWheelSpeed=>_rb!=null?Vector3.Dot(_rb.linearVelocity,transform.forward):_driveSpeed;
        private float GetWheelHealth(bool front,bool left){foreach(WheelPhysics wheel in _physicsWheels)if(wheel.front==front&&wheel.left==left)return wheel.health;return 100f;}
        public bool IsThreateningPoint(Vector3 worldPoint,float minimumKmh=30f){if(!_occupied||SpeedKmh<minimumKmh||Mathf.Abs(_driveSpeed)<.01f)return false;Vector3 toPoint=worldPoint-transform.position;toPoint.y=0;if(toPoint.sqrMagnitude<.001f)return true;toPoint.Normalize();Vector3 travelDirection=_driveSpeed>=0?transform.forward:-transform.forward;return Vector3.Dot(travelDirection,toPoint)>.35f;}
        private void Awake(){_rb=GetComponent<Rigidbody>();ApplyVehicleProfile();DetectWheelsAndScale();MakeWheelMaterialsDoubleSided();CacheBodyMaterials();ConfigureRigidbody();RebuildVehicleColliders();_bodyDamage=GetComponent<VehicleBodyDamage>();if(_bodyDamage==null)_bodyDamage=gameObject.AddComponent<VehicleBodyDamage>();_bodyDamage.Initialize(_wheelRenderers);CreateCrashEffects();}
        private void OnEnable(){ActiveCarSet.Add(this);}
        private void OnDisable(){ActiveCarSet.Remove(this);}
        private void ConfigureRigidbody(){_rb.mass=_vehicleMass;_rb.useGravity=true;_rb.isKinematic=false;_rb.constraints=RigidbodyConstraints.None;_rb.linearDamping=.018f;_rb.angularDamping=.55f;_rb.interpolation=RigidbodyInterpolation.Interpolate;_rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;_rb.maxAngularVelocity=5.5f;_rb.maxDepenetrationVelocity=1.8f;_rb.solverIterations=14;_rb.solverVelocityIterations=10;if(_chassisCollider!=null&&!float.IsInfinity(_wheelContactLocalY))_rb.centerOfMass=new Vector3(_chassisCollider.center.x,_wheelContactLocalY+_centerHeight,_chassisCollider.center.z+.04f);}
        private void ApplyVehicleProfile()
        {
            string n=name.ToLowerInvariant();
            if(n.Contains("car 04")||n.Contains("taycan")||n.Contains("porsche"))
            {
                _centerHeight=.48f;_rearDriveShare=.56f;_vehicleLabel="Porsche Taycan";_vehicleMass=2200f;topSpeed=40.28f;reverseTopSpeed=8.5f;maximumMotorTorque=2200f;serviceBrakeTorque=3400f;directionChangeBrakeTorque=3000f;maximumRoadWheelAngle=35f;wheelBase=2.62f;maximumLateralAcceleration=12.4f;aerodynamicDownforce=3.2f;
                wheelSuspensionDistance=.105f;wheelSpring=52000f;wheelDamper=7800f;suspensionTargetPosition=.72f;antiRollForce=9000f;highSpeedSteerFactor=.34f;steeringResponse=1.75f;sidewaysTyreStiffness=2.2f;tractionControlSlip=.62f;tractionTorqueFloor=.58f;minimumDamageImpactKmh=7f;crashResistance=.78f;rolloverThresholdKmh=132f;rolloverStrength=7f;impactTripStrength=.34f;_centerOfMassYOffset=-.58f;
            }
            else if(n.Contains("car 02")||n.Contains("g-class")||n.Contains("g klasse")||n.Contains("g-klasse"))
            {
                _centerHeight=.64f;_rearDriveShare=.5f;_steeringSafetyMargin=1.3f;_vehicleLabel="G-Klasse";_vehicleMass=2450f;topSpeed=30.56f;reverseTopSpeed=6.5f;maximumMotorTorque=850f;serviceBrakeTorque=2900f;directionChangeBrakeTorque=2600f;maximumRoadWheelAngle=40f;wheelBase=2.48f;maximumLateralAcceleration=8.2f;aerodynamicDownforce=1.2f;
                wheelSuspensionDistance=.20f;wheelSpring=36000f;wheelDamper=7200f;suspensionTargetPosition=.72f;antiRollForce=6200f;highSpeedSteerFactor=.38f;steeringResponse=1.55f;sidewaysTyreStiffness=1.8f;minimumDamageImpactKmh=12f;crashResistance=1.65f;rolloverThresholdKmh=78f;rolloverStrength=10f;impactTripStrength=.72f;_centerOfMassYOffset=-.30f;
            }
            else if(n.Contains("car 01")||n.Contains("skyline")||n.Contains("nissan"))
            {
                _centerHeight=.54f;_rearDriveShare=.68f;_vehicleLabel="Nissan Skyline";_vehicleMass=1560f;topSpeed=41.67f;reverseTopSpeed=8f;maximumMotorTorque=850f;serviceBrakeTorque=3000f;directionChangeBrakeTorque=2700f;maximumRoadWheelAngle=39f;wheelBase=2.42f;maximumLateralAcceleration=12.1f;aerodynamicDownforce=3f;
                wheelSuspensionDistance=.115f;wheelSpring=49000f;wheelDamper=7200f;suspensionTargetPosition=.69f;antiRollForce=8500f;highSpeedSteerFactor=.36f;steeringResponse=1.85f;sidewaysTyreStiffness=2.18f;minimumDamageImpactKmh=7.5f;crashResistance=.72f;rolloverThresholdKmh=124f;rolloverStrength=8f;impactTripStrength=.45f;_centerOfMassYOffset=-.54f;
            }
            else if(n.Contains("car 03")||n.Contains("cybertruck")||n.Contains("cyber truck"))
            {
                _centerHeight=.68f;_rearDriveShare=.52f;_vehicleLabel="Cybertruck";_vehicleMass=3000f;topSpeed=34.72f;reverseTopSpeed=7f;maximumMotorTorque=1050f;serviceBrakeTorque=3200f;directionChangeBrakeTorque=2850f;maximumRoadWheelAngle=34f;wheelBase=2.72f;maximumLateralAcceleration=9.5f;aerodynamicDownforce=1.7f;
                wheelSuspensionDistance=.18f;wheelSpring=38000f;wheelDamper=6500f;suspensionTargetPosition=.57f;antiRollForce=6500f;highSpeedSteerFactor=.34f;steeringResponse=1.45f;sidewaysTyreStiffness=2f;minimumDamageImpactKmh=14f;crashResistance=1.9f;rolloverThresholdKmh=102f;rolloverStrength=7.2f;impactTripStrength=.48f;_centerOfMassYOffset=-.48f;
            }
        }
        private void MakeWheelMaterialsDoubleSided()
        {
            foreach(Renderer wheel in _wheelRenderers)
            {
                if(wheel==null)continue;
                Material[] materials=wheel.materials;
                foreach(Material material in materials)
                {
                    if(material==null)continue;
                    if(material.HasProperty("_Cull"))material.SetFloat("_Cull",0f);
                    if(material.HasProperty("_CullMode"))material.SetFloat("_CullMode",0f);
                    if(material.HasProperty("_CullModeForward"))material.SetFloat("_CullModeForward",0f);
                    material.doubleSidedGI=true;
                }
                wheel.materials=materials;
            }
        }
        private void CacheBodyMaterials()
        {
            _bodyMaterialStates.Clear();
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if(renderer==null||_wheelRenderers.Contains(renderer))continue;
                Material[] materials=renderer.materials;
                foreach(Material material in materials)
                {
                    if(material==null)continue;int property=material.HasProperty("_BaseColor")?Shader.PropertyToID("_BaseColor"):material.HasProperty("_Color")?Shader.PropertyToID("_Color"):-1;
                    if(property<0)continue;_bodyMaterialStates.Add(new BodyMaterialState{renderer=renderer,material=material,originalColor=material.GetColor(property),colorProperty=property});
                }
                renderer.materials=materials;
            }
        }
        private void DetectWheelsAndScale()
        {
            _wheelRenderers.Clear();
            Renderer[] renderers=GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return;
            foreach(Renderer renderer in renderers)
            {
                if(renderer==null)continue;
                string n=renderer.name.ToLowerInvariant(),p=renderer.transform.parent!=null?renderer.transform.parent.name.ToLowerInvariant():"";
                if(LooksLikeWheelName(n)||LooksLikeWheelName(p))_wheelRenderers.Add(renderer);
            }
            Bounds exact=CalculatePreciseLocalBounds(renderers);
            float length=Mathf.Max(exact.size.x,exact.size.z);
            _modelScale=Mathf.Clamp(length/4.5f,.5f,3);
        }
        private static bool LooksLikeWheelName(string n)=>n.Contains("wheel")||n.Contains("tire")||n.Contains("tyre")||n.Contains("reifen")||n.Contains("felge")||n.Contains("rim")||n.Contains("roue")||n.Contains("rad_");
        private void Update(){if(!_occupied||Keyboard.current==null)return;_rawThrottle=(Keyboard.current.wKey.isPressed?1f:0f)-(Keyboard.current.sKey.isPressed?1f:0f);_rawSteer=(Keyboard.current.dKey.isPressed?1f:0f)-(Keyboard.current.aKey.isPressed?1f:0f);_brake=Keyboard.current.spaceKey.isPressed;_handbrake=Keyboard.current.leftShiftKey.isPressed;_throttle=Mathf.MoveTowards(_throttle,_rawThrottle,throttleResponse*Time.deltaTime);UpdateProgressiveSteering(Time.deltaTime);if(_ignoreExitUntilEReleased){if(!Keyboard.current.eKey.isPressed)_ignoreExitUntilEReleased=false;}else if(Keyboard.current.eKey.wasPressedThisFrame)Exit();}
        private void UpdateProgressiveSteering(float dt)
        {
            if(Mathf.Abs(_rawSteer)<.01f){_steer=Mathf.MoveTowards(_steer,0f,steeringResponse*steeringReturnMultiplier*dt);return;}
            bool reversingSteering=Mathf.Abs(_steer)>.01f&&Mathf.Sign(_steer)!=Mathf.Sign(_rawSteer);
            float speed01=Mathf.Clamp01(SpeedKmh/Mathf.Max(1f,TopSpeedKmh));
            float buildRate=steeringResponse*Mathf.Lerp(1f,highSpeedSteeringBuildMultiplier,speed01);
            if(reversingSteering)buildRate*=steeringReturnMultiplier;
            _steer=Mathf.MoveTowards(_steer,_rawSteer,buildRate*dt);
        }
        private void FixedUpdate()
        {
            _vehicleDisabled=engineHealth<=.01f;
            float dt=Time.fixedDeltaTime;
            if(_physicsWheels.Count>=4){FixedUpdateWheelPhysics(dt);return;}
            StabilizeAcrossSmallBumps();
            if(!_occupied)return;

            if(_brake)_driveSpeed=Mathf.MoveTowards(_driveSpeed,0f,brakeAcceleration*dt);
            else if(_throttle>.01f)
            {
                float rate=_driveSpeed<0f?brakeAcceleration:forwardAcceleration;
                _driveSpeed=Mathf.MoveTowards(_driveSpeed,topSpeed,rate*Mathf.Max(.22f,_throttle)*dt);
            }
            else if(_throttle<-.01f)
            {
                float rate=_driveSpeed>0f?brakeAcceleration:reverseAcceleration;
                _driveSpeed=Mathf.MoveTowards(_driveSpeed,-reverseTopSpeed,rate*Mathf.Max(.22f,-_throttle)*dt);
            }
            else _driveSpeed=Mathf.MoveTowards(_driveSpeed,0f,(engineBraking+rollingResistance*Mathf.Abs(_driveSpeed))*dt);

            Vector3 local=transform.InverseTransformDirection(_rb.linearVelocity);
            // If physics stopped the car, changing direction starts from its real motion
            // instead of waiting for an invisible full-speed motor value to count down.
            if(_throttle<-.1f&&_driveSpeed>0f&&local.z<.5f)_driveSpeed=Mathf.Max(0f,local.z);
            else if(_throttle>.1f&&_driveSpeed<0f&&local.z>-.5f)_driveSpeed=Mathf.Min(0f,local.z);
            float speed=Mathf.Abs(local.z),speed01=Mathf.Clamp01(speed/topSpeed);
            float grip=Mathf.Lerp(lateralGripLowSpeed,lateralGripHighSpeed,speed01);
            local.x=Mathf.MoveTowards(local.x,0f,grip*dt);
            local.z=Mathf.MoveTowards(local.z,_driveSpeed,(forwardAcceleration+5.5f)*dt);
            Vector3 wanted=transform.TransformDirection(new Vector3(local.x,0f,local.z));
            wanted.y=_rb.linearVelocity.y;
            _rb.linearVelocity=wanted;

            // A small virtual crawl speed gives immediate steering while setting off,
            // without allowing full arcade-style rotation on the spot.
            float steeringSpeed=Mathf.Max(speed,Mathf.Abs(_throttle)*.45f);
            float authority=Mathf.Clamp01(steeringSpeed/.58f);
            float steerMult=Mathf.Lerp(1f,highSpeedSteerFactor,speed01);
            float roadWheelAngle=_steer*maximumRoadWheelAngle*steerMult;
            _visualSteeringInput=maximumRoadWheelAngle>.01f?roadWheelAngle/maximumRoadWheelAngle:0f;
            float targetYawRad=Mathf.Tan(roadWheelAngle*Mathf.Deg2Rad)*steeringSpeed/Mathf.Max(1.2f,wheelBase);
            float lateralLimit=maximumLateralAcceleration/Mathf.Max(steeringSpeed,1f);
            targetYawRad=Mathf.Clamp(targetYawRad,-lateralLimit,lateralLimit);
            if(_driveSpeed<-.05f)targetYawRad=-targetYawRad;
            float targetYawRateDeg=targetYawRad*Mathf.Rad2Deg*authority;
            _yawRate=Mathf.MoveTowards(_yawRate,targetYawRateDeg,steeringYawAcceleration*dt);
            if(Mathf.Abs(_steer)<.01f)_yawRate=Mathf.MoveTowards(_yawRate,0f,steeringYawAcceleration*1.45f*dt);

            Vector3 av=_rb.angularVelocity;
            av.y=_yawRate*Mathf.Deg2Rad;
            av.x*=.48f;
            av.z*=.48f;
            _rb.angularVelocity=av;

            DetectNPCHits();
            DetectNPCOverrun();
            _debugTimer+=dt;
            if(_debugTimer>=1f&&Mathf.Abs(_rawThrottle)>.1f)
            {
                _debugTimer=0;
                Debug.Log($"[CYDOY] CAR speed={SpeedKmh:F1}km/h throttle={_throttle:F2} wheelAngle={roadWheelAngle:F1} yawRate={_yawRate:F1}",this);
            }
        }

        private void FixedUpdateWheelPhysics(float dt)
        {
            _rb.useGravity=true;
            float forwardSpeed=Vector3.Dot(_rb.linearVelocity,transform.forward);
            _driveSpeed=forwardSpeed;
            UpdateDrivetrainTelemetry(forwardSpeed,dt);
            float engine01=Mathf.Clamp01(engineHealth/100f);
            // Damage remains readable in the HUD, but a non-destroyed drivetrain
            // always retains enough force to move the car. Only zero engine health
            // is a hard mechanical shutdown.
            float enginePerformance=_vehicleDisabled?0f:Mathf.Lerp(.72f,1f,engine01);
            float effectiveTopSpeed=topSpeed*enginePerformance;
            float speed01=Mathf.Clamp01(Mathf.Abs(forwardSpeed)/Mathf.Max(.1f,effectiveTopSpeed));
            float requestedWheelAngle=maximumRoadWheelAngle*Mathf.Lerp(1f,highSpeedSteerFactor,speed01);
            if(Mathf.Abs(forwardSpeed)>3f)
            {
                float safeAngle=Mathf.Atan(maximumLateralAcceleration*wheelBase/Mathf.Max(1f,forwardSpeed*forwardSpeed))*Mathf.Rad2Deg*_steeringSafetyMargin;
                float minimumUsefulAngle=1.5f;
                requestedWheelAngle=Mathf.Min(requestedWheelAngle,Mathf.Max(minimumUsefulAngle,safeAngle));
            }
            float lateralSpeed=Vector3.Dot(_rb.linearVelocity,transform.right);
            float slipAngle=Mathf.Atan2(lateralSpeed,Mathf.Max(1f,Mathf.Abs(forwardSpeed)))*Mathf.Rad2Deg;
            int groundedWheels=0;
            foreach(WheelPhysics wheel in _physicsWheels)
                if(wheel.collider!=null&&wheel.collider.isGrounded)groundedWheels++;
            bool driftContact=groundedWheels>=3&&forwardSpeed>5f&&Vector3.Dot(transform.up,Vector3.up)>.85f;
            // Preserve countersteering authority while sliding: high-speed turn
            // limiting must not prevent the driver from catching the rear axle.
            if(driftContact&&Mathf.Abs(slipAngle)>5f&&_steer*lateralSpeed>0f)
                requestedWheelAngle=Mathf.Max(requestedWheelAngle,Mathf.Min(maximumRoadWheelAngle,Mathf.Abs(slipAngle)+6f));
            float roadWheelAngle=_steer*requestedWheelAngle;
            _visualSteeringInput=maximumRoadWheelAngle>.01f?roadWheelAngle/maximumRoadWheelAngle:0f;
            bool changingDirection=(_throttle>.08f&&forwardSpeed<-.35f)||(_throttle<-.08f&&forwardSpeed>.35f);
            bool handbrakeDrift=_occupied&&_handbrake;
            // Slip, rather than the simulated gear/RPM cycle, sustains a drift.
            bool powerDrift=_occupied&&driftContact&&!_brake&&_throttle>.55f&&
                ((_driftBlend>.15f&&Mathf.Abs(slipAngle)>7f)||
                 (_rearDriveShare>.6f&&_throttle>.85f&&Mathf.Abs(_steer)>.5f&&forwardSpeed>9f));
            float driftTarget=_occupied&&driftContact&&(handbrakeDrift||powerDrift)?1f:0f;
            _driftBlend=Mathf.MoveTowards(_driftBlend,driftTarget,(driftTarget>_driftBlend?2.4f:1.1f)*dt);
            float driftGrip=Mathf.Lerp(.82f,.68f,Mathf.InverseLerp(.5f,.68f,_rearDriveShare));
            float targetRearGrip=Mathf.Lerp(1f,handbrakeDrift?.52f:driftGrip,_driftBlend);
            // Beyond a large slip angle restore grip gradually instead of
            // encouraging an endless spin. No velocity or yaw is injected.
            targetRearGrip=Mathf.Lerp(targetRearGrip,1f,Mathf.InverseLerp(40f,75f,Mathf.Abs(slipAngle)));
            _rearGripMultiplier=Mathf.MoveTowards(_rearGripMultiplier,targetRearGrip,1.5f*dt);
            _isDrifting=driftContact&&Mathf.Abs(slipAngle)>8f;
            float torque=0f,brake=0f;
            if(!_occupied)brake=parkBrakeTorque;
            else if(_brake)brake=serviceBrakeTorque;
            else if(changingDirection)brake=directionChangeBrakeTorque;
            else
            {
                bool belowLimit=!_vehicleDisabled&&(_throttle>=0f?forwardSpeed<effectiveTopSpeed:forwardSpeed>-reverseTopSpeed);
                if(belowLimit)
                {
                    float directionLimit=_throttle>=0f?topSpeed:reverseTopSpeed;
                    float driveSpeed01=Mathf.Clamp01(Mathf.Abs(forwardSpeed)/Mathf.Max(.1f,directionLimit));
                    float taper=Mathf.Lerp(1f,.24f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.55f,1f,driveSpeed01)));
                    torque=_throttle*maximumMotorTorque*taper*enginePerformance;
                }
                // Let the tyres coast naturally when W/S is released. Only Space,
                // a direction change or a real obstacle applies braking torque.
                if(Mathf.Abs(_throttle)<.04f)brake=0f;
            }
            if(Vector3.Dot(transform.up,Vector3.up)<.45f)torque=0f;
            foreach(WheelPhysics wheel in _physicsWheels)
            {
                if(wheel.collider==null)continue;
                wheel.collider.steerAngle=wheel.front?roadWheelAngle:0f;
                float wheelTorque=torque*(wheel.front?(1f-_rearDriveShare)*2f:_rearDriveShare*2f);
                if(wheel.collider.GetGroundHit(out WheelHit tractionHit))
                {
                    float slip=Mathf.Abs(tractionHit.forwardSlip);
                    if(slip>tractionControlSlip)wheelTorque*=Mathf.Lerp(1f,tractionTorqueFloor,Mathf.InverseLerp(tractionControlSlip,1.35f,slip));
                }
                float wheelHealth01=Mathf.Clamp01(wheel.health/100f);
                wheel.collider.motorTorque=handbrakeDrift&&!wheel.front?0f:wheelTorque*Mathf.Lerp(.78f,1f,wheelHealth01);
                float wheelBrake=brake*(wheel.front?1.2f:.8f);
                if(Mathf.Abs(forwardSpeed)>4f&&wheel.collider.GetGroundHit(out WheelHit brakeHit)&&Mathf.Abs(brakeHit.forwardSlip)>.5f)wheelBrake*=.4f;
                if(handbrakeDrift&&!wheel.front)wheelBrake=Mathf.Max(wheelBrake,serviceBrakeTorque*1.1f);
                wheel.collider.brakeTorque=wheelBrake;
                WheelFrictionCurve forward=wheel.collider.forwardFriction;
                forward.stiffness=1.35f;
                wheel.collider.forwardFriction=forward;
                WheelFrictionCurve side=wheel.collider.sidewaysFriction;
                side.stiffness=sidewaysTyreStiffness*(wheel.front?1f:_rearGripMultiplier)*Mathf.Lerp(.48f,1f,wheelHealth01);
                wheel.collider.sidewaysFriction=side;
                JointSpring damagedSpring=wheel.collider.suspensionSpring;damagedSpring.spring=wheelSpring*Mathf.Lerp(.82f,1f,wheelHealth01);damagedSpring.damper=wheelDamper*Mathf.Lerp(.86f,1f,wheelHealth01);wheel.collider.suspensionSpring=damagedSpring;
            }
            if(_occupied&&Mathf.Abs(_throttle)>.05f&&!_vehicleDisabled)_rb.WakeUp();
            if(!_rolloverActive&&Vector3.Dot(transform.up,Vector3.up)>.85f)_rb.AddForce(-transform.up*(aerodynamicDownforce*Mathf.Abs(forwardSpeed)*Mathf.Abs(forwardSpeed)),ForceMode.Force);
            // Torque tapers at the speed limit; retain collision and downhill momentum.
            ApplyBodyGroundDrag(dt);
            ApplyHighSpeedRollover(forwardSpeed,roadWheelAngle);
            ApplyPostRolloverRighting(dt);
            ApplyAntiRoll(true);ApplyAntiRoll(false);
            if(!_occupied)return;
            DetectNPCHits();DetectNPCOverrun();
            _debugTimer+=dt;
            if(_debugTimer>=1f&&Mathf.Abs(_rawThrottle)>.1f){_debugTimer=0;Debug.Log($"[CYDOY] CAR speed={SpeedKmh:F1}km/h throttle={_throttle:F2} wheelAngle={roadWheelAngle:F1}",this);}
        }

        private void LimitLongitudinalSpeed(float effectiveTopSpeed)
        {
            if(_rb==null||effectiveTopSpeed<=0f)return;
            Vector3 localVelocity=transform.InverseTransformDirection(_rb.linearVelocity);
            float allowedForward=effectiveTopSpeed*1.015f;
            if(localVelocity.z>allowedForward)localVelocity.z=allowedForward;
            else if(localVelocity.z<-reverseTopSpeed*1.015f)localVelocity.z=-reverseTopSpeed*1.015f;
            _rb.linearVelocity=transform.TransformDirection(localVelocity);
        }

        private void UpdateDrivetrainTelemetry(float forwardSpeed,float dt)
        {
            float kmh=Mathf.Abs(forwardSpeed)*3.6f,targetRpm;
            if(forwardSpeed<-.5f)
            {
                _currentGear=-1;
                targetRpm=Mathf.Lerp(1100f,6200f,Mathf.Clamp01(kmh/Mathf.Max(1f,reverseTopSpeed*3.6f)));
            }
            else
            {
                float normalized=Mathf.Clamp01(kmh/Mathf.Max(1f,topSpeed*3.6f));
                _currentGear=Mathf.Clamp(Mathf.FloorToInt(normalized*6f)+1,1,6);
                float gearStart=(_currentGear-1)/6f;
                float gearProgress=Mathf.InverseLerp(gearStart,Mathf.Min(1f,gearStart+1f/6f),normalized);
                targetRpm=Mathf.Lerp(1150f,7000f,gearProgress)+Mathf.Abs(_throttle)*180f;
            }
            if(Mathf.Abs(forwardSpeed)<.25f)targetRpm=900f+Mathf.Abs(_throttle)*1500f;
            _engineRpm=Mathf.Lerp(_engineRpm,Mathf.Clamp(targetRpm,850f,7200f),1f-Mathf.Exp(-8f*dt));
        }

        private void ApplyAntiRoll(bool frontAxle)
        {
            if(Vector3.Dot(transform.up,Vector3.up)<.55f)return;
            WheelPhysics left=null,right=null;
            foreach(WheelPhysics wheel in _physicsWheels){if(wheel.front!=frontAxle)continue;if(wheel.left)left=wheel;else right=wheel;}
            if(left==null||right==null||left.collider==null||right.collider==null)return;
            float leftTravel=1f,rightTravel=1f;
            bool leftGrounded=left.collider.GetGroundHit(out WheelHit leftHit),rightGrounded=right.collider.GetGroundHit(out WheelHit rightHit);
            if(leftGrounded)leftTravel=(-left.collider.transform.InverseTransformPoint(leftHit.point).y-left.collider.radius)/Mathf.Max(.01f,left.collider.suspensionDistance);
            if(rightGrounded)rightTravel=(-right.collider.transform.InverseTransformPoint(rightHit.point).y-right.collider.radius)/Mathf.Max(.01f,right.collider.suspensionDistance);
            // Bound anti-roll force by axle weight so it cannot pin an unloaded tyre.
            float stabilizerFactor=1f;
            float force=(Mathf.Clamp01(leftTravel)-Mathf.Clamp01(rightTravel))*antiRollForce*stabilizerFactor;
            force=Mathf.Clamp(force,-_rb.mass*Physics.gravity.magnitude*.22f,_rb.mass*Physics.gravity.magnitude*.22f);
            if(leftGrounded)_rb.AddForceAtPosition(left.collider.transform.up*-force,left.collider.transform.position);
            if(rightGrounded)_rb.AddForceAtPosition(right.collider.transform.up*force,right.collider.transform.position);
        }

        private void ApplyHighSpeedRollover(float forwardSpeed,float roadWheelAngle)
        {
            // Observe the physical roll. Steering does not inject extra launch,
            // lift or rotation energy into a car that has already lost traction.
            float upright=Vector3.Dot(transform.up,Vector3.up);
            if(!_rolloverActive&&upright<.65f&&_rb.linearVelocity.sqrMagnitude>9f)
            {
                _rolloverActive=true;
                _rolloverElapsed=0f;
                _rolloverGroundHits=0;
                _rolloverDirection=Mathf.Sign(Vector3.Dot(_rb.angularVelocity,transform.forward));
            }
            _rolloverImpactCooldown=Mathf.Max(0f,_rolloverImpactCooldown-Time.fixedDeltaTime);
            if(!_rolloverActive)return;
            _rolloverElapsed+=Time.fixedDeltaTime;
            if(_rolloverElapsed>.6f&&
               (upright>.9f||(_rb.angularVelocity.sqrMagnitude<.16f&&_rb.linearVelocity.sqrMagnitude<1f)))
            {
                _rolloverActive=false;
                _postRolloverRestTime=0f;
            }
        }

        private void ApplyBodyGroundDrag(float dt)
        {
            if(Time.fixedTime>_bodyContactUntil)return;
            // Contact friction opposes sliding only. Airborne momentum and the
            // normal component of the collision response are preserved.
            Vector3 slide=Vector3.ProjectOnPlane(_rb.linearVelocity,_bodyContactNormal);
            float speed=slide.magnitude;
            if(speed<.01f)return;
            float deceleration=Mathf.Min(speed/Mathf.Max(.001f,dt),Physics.gravity.magnitude*.38f);
            _rb.AddForce(-slide/speed*deceleration,ForceMode.Acceleration);
        }

        private void ApplyPostRolloverRighting(float dt)
        {
            if(_rolloverActive||!_occupied||Time.fixedTime>_bodyContactUntil)return;
            float upright=Vector3.Dot(transform.up,Vector3.up);
            if(upright>.88f||_rb.linearVelocity.sqrMagnitude>1f||_rb.angularVelocity.sqrMagnitude>.25f){_postRolloverRestTime=0f;return;}
            _postRolloverRestTime+=dt;
            if(_postRolloverRestTime<2.5f||Mathf.Abs(_rawSteer)<.5f)return;
            Vector3 axis=Vector3.Cross(transform.up,Vector3.up);
            if(axis.sqrMagnitude<.01f)axis=transform.forward*Mathf.Sign(_rolloverDirection==0f?1f:_rolloverDirection);
            _rb.AddTorque(axis.normalized*4.2f,ForceMode.Acceleration);
        }

        private void LateUpdate()
        {
            if(_occupied&&_driver!=null){Transform seat=driverSeat!=null?driverSeat:transform;_driver.SetPositionAndRotation(seat.position,seat.rotation);}
            foreach(WheelPhysics wheel in _physicsWheels)
            {
                if(wheel.collider==null||wheel.steeringPivot==null||wheel.spinPivot==null)continue;
                wheel.collider.GetWorldPose(out Vector3 position,out _);
                wheel.steeringPivot.position=position-transform.up*((wheelVisualGroundInset+Mathf.InverseLerp(100f,0f,wheel.health)*.035f)*_modelScale);
                wheel.steeringPivot.localRotation=wheel.steeringBaseRotation*Quaternion.Euler(0f,wheel.front?wheel.collider.steerAngle:0f,0f);
                wheel.spinDegrees=Mathf.Repeat(wheel.spinDegrees+wheel.collider.rpm*6f*Time.deltaTime,360f);
                wheel.spinPivot.localRotation=wheel.spinBaseRotation*Quaternion.Euler(wheel.spinDegrees,0f,0f);
            }
        }

        private void StabilizeAcrossSmallBumps()
        {
            _smoothRoadContact=false;
            if(_wheelSupportColliders.Count<2){_rb.useGravity=true;return;}
            float lowest=float.PositiveInfinity,highest=float.NegativeInfinity;
            float verticalCorrection=0f;
            int hits=0;
            foreach(SphereCollider wheel in _wheelSupportColliders)
            {
                if(wheel==null)continue;
                float supportScale=Mathf.Max(Mathf.Abs(wheel.transform.lossyScale.x),Mathf.Max(Mathf.Abs(wheel.transform.lossyScale.y),Mathf.Abs(wheel.transform.lossyScale.z)));
                float radiusWorld=wheel.radius*Mathf.Max(.0001f,supportScale);
                Vector3 origin=wheel.transform.position+Vector3.up*(radiusWorld+.28f*_modelScale);
                RaycastHit[] rayHits=Physics.RaycastAll(origin,Vector3.down,radiusWorld+1.05f*_modelScale,~0,QueryTriggerInteraction.Ignore);
                bool found=false;
                RaycastHit groundHit=default;
                float bestDistance=float.PositiveInfinity;
                foreach(RaycastHit candidate in rayHits)
                {
                    if(candidate.transform==transform||candidate.transform.IsChildOf(transform)||candidate.normal.y<.45f)continue;
                    if(candidate.distance>=bestDistance)continue;
                    bestDistance=candidate.distance;
                    groundHit=candidate;
                    found=true;
                }
                if(!found)continue;
                lowest=Mathf.Min(lowest,groundHit.point.y);
                highest=Mathf.Max(highest,groundHit.point.y);
                float wheelBottom=wheel.transform.position.y-radiusWorld;
                verticalCorrection+=groundHit.point.y+tyreGroundClearance-wheelBottom;
                hits++;
            }
            if(hits<2){_rb.useGravity=true;return;}
            _rb.useGravity=false;
            verticalCorrection/=hits;
            float activationHeight=Mathf.Max(.025f,Mathf.Min(smallBumpHeightLimit,curbReferenceHeight*suspensionActivationPercent)*_modelScale);
            bool curbTransition=highest-lowest>activationHeight;
            _smoothRoadContact=!curbTransition;
            float followResponse=curbTransition?12f:60f;
            float followBlend=1f-Mathf.Exp(-followResponse*Time.fixedDeltaTime);
            float heightStep=Mathf.Clamp(verticalCorrection*followBlend,-.16f*_modelScale,.16f*_modelScale);
            Vector3 groundedPosition=_rb.position;groundedPosition.y+=heightStep;_rb.position=groundedPosition;
            Vector3 velocity=_rb.linearVelocity;velocity.y=0f;_rb.linearVelocity=velocity;
            Vector3 angular=_rb.angularVelocity;angular.x=0f;angular.z=0f;_rb.angularVelocity=angular;
        }

        private bool TallObstacleAtBumper(int direction)
        {
            if(_chassisCollider==null||direction==0)return false;
            Vector3 size=_chassisCollider.size;
            Vector3 center=_chassisCollider.center;
            float contactY=float.IsInfinity(_wheelContactLocalY)?center.y-size.y*.5f:_wheelContactLocalY;
            Vector3 localOrigin=new(center.x,contactY+blockingObstacleHeight*_modelScale,center.z+direction*(size.z*.5f+.035f*_modelScale));
            Vector3 origin=transform.TransformPoint(localOrigin);
            Vector3 halfExtents=new(size.x*.42f,.028f*_modelScale,.025f*_modelScale);
            RaycastHit[] hits=Physics.BoxCastAll(origin,halfExtents,direction*transform.forward,transform.rotation,bumperProbeDistance*_modelScale,~0,QueryTriggerInteraction.Ignore);
            foreach(RaycastHit hit in hits)
            {
                if(hit.collider==null||hit.transform==transform||hit.transform.IsChildOf(transform)||Mathf.Abs(hit.normal.y)>.55f)continue;
                if(hit.collider.GetComponentInParent<NPCWanderer>()!=null)continue;
                return true;
            }
            return false;
        }

        private void ProcessCrashDamage(Collision collision)
        {
            if(collision==null||collision.collider==null||collision.contactCount==0)return;
            if(collision.collider.GetComponentInParent<NPCWanderer>()!=null)return;
            if(collision.collider==_lastCrashCollider&&Time.time-_lastCrashTime<.22f)return;
            Vector3 relativeVelocity=collision.relativeVelocity;
            float closingSpeed=0f;Vector3 impactPoint=Vector3.zero,separationNormal=Vector3.zero;int validContacts=0;
            foreach(ContactPoint contact in collision.contacts)
            {
                Vector3 localPoint=transform.InverseTransformPoint(contact.point);
                if(contact.thisCollider!=_chassisCollider)continue;
                if(Vector3.Dot(transform.up,Vector3.up)>.75f&&(contact.normal.y>.58f||localPoint.y<=_wheelContactLocalY+.09f*_modelScale))continue;
                closingSpeed=Mathf.Max(closingSpeed,Mathf.Abs(Vector3.Dot(relativeVelocity,contact.normal)));
                impactPoint+=contact.point;separationNormal+=contact.normal;validContacts++;
            }
            if(validContacts==0)return;
            impactPoint/=validContacts;
            float impactKmh=closingSpeed*3.6f;
            if(impactKmh<minimumDamageImpactKmh)return;
            _lastCrashCollider=collision.collider;_lastCrashTime=Time.time;

            Vector3 localImpact=transform.InverseTransformPoint(impactPoint);
            Vector3 center=_chassisCollider!=null?_chassisCollider.center:Vector3.zero;
            Vector3 size=_chassisCollider!=null?_chassisCollider.size:new Vector3(2f,1f,4f);
            float longitudinal=(localImpact.z-center.z)/Mathf.Max(.1f,size.z*.5f);
            float lateral=Mathf.Abs(localImpact.x-center.x)/Mathf.Max(.1f,size.x*.5f);
            bool front=longitudinal>.28f,rear=longitudinal<-.28f,side=lateral>.62f;
            DriveableCar otherCar=collision.collider.GetComponentInParent<DriveableCar>();
            float zoneMultiplier=side ? 1.16f : rear ? .78f : 1f;
            float damage=Mathf.Clamp(Mathf.Pow(Mathf.Max(0f,impactKmh-minimumDamageImpactKmh),1.1f)*.55f*zoneMultiplier/Mathf.Max(.35f,crashResistance),0f,100f);
            bodyHealth=Mathf.Max(0f,bodyHealth-damage);
            float engineDamage=damage*(front ? .48f : side ? .14f : .06f);
            engineHealth=Mathf.Max(0f,engineHealth-engineDamage);
            float panelDamage=damage*((front||rear) ? .92f : .72f);
            if(front)frontBodyHealth=Mathf.Max(0f,frontBodyHealth-panelDamage);
            if(rear)rearBodyHealth=Mathf.Max(0f,rearBodyHealth-panelDamage);
            if(side)
            {
                if(localImpact.x<center.x)leftBodyHealth=Mathf.Max(0f,leftBodyHealth-panelDamage);
                else rightBodyHealth=Mathf.Max(0f,rightBodyHealth-panelDamage);
            }
            if(!front&&!rear&&!side)
            {
                leftBodyHealth=Mathf.Max(0f,leftBodyHealth-damage*.32f);
                rightBodyHealth=Mathf.Max(0f,rightBodyHealth-damage*.32f);
            }

            if(damage>7f&&_physicsWheels.Count>0)
            {
                WheelPhysics nearest=null;float nearestSqr=float.PositiveInfinity;
                foreach(WheelPhysics wheel in _physicsWheels)
                {
                    if(wheel.collider==null)continue;
                    float sqr=(wheel.collider.transform.position-impactPoint).sqrMagnitude;
                    if(sqr<nearestSqr){nearestSqr=sqr;nearest=wheel;}
                }
                if(nearest!=null)nearest.health=Mathf.Max(0f,nearest.health-damage*(side ? .92f : .48f));
            }

            _vehicleDisabled=engineHealth<=.01f;
            if(_vehicleDisabled){_throttle=0f;_rawThrottle=0f;}
            if(_occupied&&impactKmh>34f&&_driver!=null)
            {
                CheatOnYourDayOnes.Player.PlayerAgent player=_driver.GetComponent<CheatOnYourDayOnes.Player.PlayerAgent>();
                if(player!=null&&player.Needs!=null)player.Needs.RequestDamage(Mathf.Clamp((impactKmh-30f)*.22f,0f,32f));
            }
            UpdateDamageEffects(impactPoint,damage);
            ApplyLocalizedBodyDamage(impactPoint,damage);
            CreateImpactScar(impactPoint,separationNormal.sqrMagnitude>.001f?separationNormal.normalized:(transform.position-impactPoint).normalized,damage);
            UpdateVisibleBodyDamage();
            string zone=front?"front":rear?"rear":side?"side":"corner";
            Debug.Log($"[CYDOY] CRASH {impactKmh:F0} km/h | {zone} | damage {damage:F0} | body {bodyHealth:F0}% | engine {engineHealth:F0}%",this);
        }

        private void CreateCrashEffects()
        {
            GameObject sparks=new("Crash Sparks");sparks.transform.SetParent(transform,false);
            _crashParticles=sparks.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule sparkMain=_crashParticles.main;sparkMain.playOnAwake=false;sparkMain.loop=false;sparkMain.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.5f);sparkMain.startSpeed=new ParticleSystem.MinMaxCurve(2f,6f);sparkMain.startSize=new ParticleSystem.MinMaxCurve(.025f,.075f);sparkMain.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,.9f,.3f,1f),new Color(1f,.22f,.02f,1f));sparkMain.gravityModifier=1.15f;sparkMain.simulationSpace=ParticleSystemSimulationSpace.World;sparkMain.maxParticles=80;
            ParticleSystem.EmissionModule sparkEmission=_crashParticles.emission;sparkEmission.enabled=false;_crashParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer sparkRenderer=sparks.GetComponent<ParticleSystemRenderer>();sparkRenderer.renderMode=ParticleSystemRenderMode.Stretch;sparkRenderer.lengthScale=2.4f;sparkRenderer.velocityScale=.12f;sparkRenderer.sharedMaterial=CreateParticleMaterial("CYDOY Crash Sparks",false);if(sparkRenderer.sharedMaterial==null)sparkRenderer.enabled=false;

            GameObject smoke=new("Engine Damage Smoke");smoke.transform.SetParent(transform,false);
            Vector3 smokePosition=_chassisCollider!=null?_chassisCollider.center+new Vector3(0f,_chassisCollider.size.y*.48f,_chassisCollider.size.z*.3f):new Vector3(0f,.8f,1f);smoke.transform.localPosition=smokePosition;
            _damageSmoke=smoke.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule smokeMain=_damageSmoke.main;smokeMain.playOnAwake=false;smokeMain.loop=true;smokeMain.startLifetime=new ParticleSystem.MinMaxCurve(2.1f,3.8f);smokeMain.startSpeed=new ParticleSystem.MinMaxCurve(.06f,.18f);smokeMain.startSize=new ParticleSystem.MinMaxCurve(.18f,.38f);smokeMain.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);smokeMain.startColor=new ParticleSystem.MinMaxGradient(new Color(.18f,.18f,.18f,.38f),new Color(.46f,.46f,.46f,.19f));smokeMain.simulationSpace=ParticleSystemSimulationSpace.World;smokeMain.maxParticles=70;
            ParticleSystem.EmissionModule smokeEmission=_damageSmoke.emission;smokeEmission.rateOverTime=0f;_damageSmoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.ShapeModule smokeShape=_damageSmoke.shape;smokeShape.shapeType=ParticleSystemShapeType.Cone;smokeShape.angle=10f;smokeShape.radius=.08f*_modelScale;
            ParticleSystem.SizeOverLifetimeModule smokeSize=_damageSmoke.sizeOverLifetime;smokeSize.enabled=true;smokeSize.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0f,.35f),new Keyframe(.24f,.78f),new Keyframe(1f,1.65f)));
            ParticleSystem.ColorOverLifetimeModule smokeColor=_damageSmoke.colorOverLifetime;smokeColor.enabled=true;Gradient smokeGradient=new();smokeGradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(new Color(.72f,.72f,.72f),1f)},new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.68f,.16f),new GradientAlphaKey(.38f,.64f),new GradientAlphaKey(0f,1f)});smokeColor.color=smokeGradient;
            ParticleSystem.NoiseModule smokeNoise=_damageSmoke.noise;smokeNoise.enabled=true;smokeNoise.strength=new ParticleSystem.MinMaxCurve(.035f,.105f);smokeNoise.frequency=.22f;smokeNoise.scrollSpeed=.14f;smokeNoise.quality=ParticleSystemNoiseQuality.High;
            ParticleSystem.VelocityOverLifetimeModule smokeVelocity=_damageSmoke.velocityOverLifetime;smokeVelocity.enabled=true;smokeVelocity.space=ParticleSystemSimulationSpace.World;smokeVelocity.x=new ParticleSystem.MinMaxCurve(0f);smokeVelocity.y=new ParticleSystem.MinMaxCurve(.18f);smokeVelocity.z=new ParticleSystem.MinMaxCurve(0f);
            ParticleSystemRenderer smokeRenderer=smoke.GetComponent<ParticleSystemRenderer>();smokeRenderer.renderMode=ParticleSystemRenderMode.Billboard;smokeRenderer.sharedMaterial=CreateParticleMaterial("CYDOY Engine Smoke",true);if(smokeRenderer.sharedMaterial==null)smokeRenderer.enabled=false;
        }

        private static Material CreateParticleMaterial(string materialName,bool smoke)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if(shader==null)shader=Shader.Find("Particles/Standard Unlit");
            if(shader==null)return null;
            Material material=new(shader){name=materialName,renderQueue=3000};
            material.SetOverrideTag("RenderType","Transparent");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.EnableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1f);
            if(material.HasProperty("_Blend"))material.SetFloat("_Blend",0f);
            if(material.HasProperty("_AlphaClip"))material.SetFloat("_AlphaClip",0f);
            if(material.HasProperty("_ZWrite"))material.SetFloat("_ZWrite",0f);
            if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if(material.HasProperty("_SrcBlendAlpha"))material.SetFloat("_SrcBlendAlpha",(float)UnityEngine.Rendering.BlendMode.One);
            if(material.HasProperty("_DstBlendAlpha"))material.SetFloat("_DstBlendAlpha",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
            Texture2D texture=smoke?Resources.Load<Texture2D>("VFX/CleanEngineSmoke"):CreateSoftParticleTexture(24,false);
            if(texture==null)texture=CreateSoftParticleTexture(smoke?64:24,smoke);
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
            if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
            return material;
        }

        private static Texture2D CreateSoftParticleTexture(int size,bool smoky)
        {
            Texture2D texture=new(size,size,TextureFormat.RGBA32,false){name=smoky?"CYDOY Soft Smoke":"CYDOY Soft Spark",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            Color32[] pixels=new Color32[size*size];Vector2 center=new((size-1)*.5f,(size-1)*.5f);float radius=size*.5f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){float distance=Vector2.Distance(new Vector2(x,y),center)/radius;float alpha=Mathf.Pow(Mathf.Clamp01(1f-distance),smoky?1.7f:.55f);pixels[y*size+x]=new Color32(255,255,255,(byte)(alpha*255f));}
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }

        private void UpdateDamageEffects(Vector3 impactPoint,float damage)
        {
            if(_crashParticles!=null&&damage>1f){_crashParticles.transform.position=impactPoint;ParticleSystem.EmissionModule emission=_crashParticles.emission;emission.enabled=true;_crashParticles.Emit(Mathf.Clamp(Mathf.RoundToInt(damage*.45f),4,36));emission.enabled=false;}
            if(_damageSmoke==null)return;
            float severity=1f-engineHealth/100f;
            ParticleSystem.EmissionModule smokeEmission=_damageSmoke.emission;smokeEmission.rateOverTime=severity>.45f?Mathf.Lerp(2.2f,9f,Mathf.InverseLerp(.45f,1f,severity)):0f;
            if(severity>.45f&&!_damageSmoke.isPlaying)_damageSmoke.Play();else if(severity<=.45f&&_damageSmoke.isPlaying)_damageSmoke.Stop();
        }

        private void UpdateVisibleBodyDamage() { }
        private void ApplyLocalizedBodyDamage(Vector3 impactPoint,float damage) { }
        private void CreateImpactScar(Vector3 point,Vector3 inward,float damage)
        {
            if(_bodyDamage!=null)_bodyDamage.ApplyImpact(point,inward,damage);
        }

        private void OnCollisionEnter(Collision collision){RecordBodyContact(collision);ProcessCrashDamage(collision);SuppressRoadTriangleImpulse(collision);}
        private void OnCollisionStay(Collision collision){RecordBodyContact(collision);SuppressRoadTriangleImpulse(collision);}


        private void RecordBodyContact(Collision collision)
        {
            if(collision==null||Vector3.Dot(transform.up,Vector3.up)>.82f)return;
            for(int i=0;i<collision.contactCount;i++)
            {
                ContactPoint contact=collision.GetContact(i);
                if(contact.thisCollider!=_chassisCollider||contact.normal.y<.3f)continue;
                _bodyContactNormal=contact.normal.normalized;
                _bodyContactUntil=Time.fixedTime+Time.fixedDeltaTime*1.5f;
                return;
            }
        }

        private void SuppressRoadTriangleImpulse(Collision collision)
        {
            // During a rollover these are genuine body-to-ground impulses. The old
            // seam filter reduced roll velocity by 80% on every hit and trapped the
            // vehicle on its side.
            if(_rolloverActive||!_smoothRoadContact||collision==null||_rb==null||_rb.linearVelocity.y<=0f)return;
            float seamLimit=Mathf.Max(.025f,Mathf.Min(smallBumpHeightLimit,curbReferenceHeight*suspensionActivationPercent)*_modelScale);
            bool lowRoadContact=false;
            foreach(ContactPoint contact in collision.contacts)
            {
                Vector3 local=transform.InverseTransformPoint(contact.point);
                if(contact.normal.y>.35f||local.y<=_wheelContactLocalY+seamLimit){lowRoadContact=true;break;}
            }
            if(!lowRoadContact)return;
            Vector3 velocity=_rb.linearVelocity;velocity.y=0f;_rb.linearVelocity=velocity;
            Vector3 angular=_rb.angularVelocity;angular.x*=.2f;angular.z*=.2f;_rb.angularVelocity=angular;
        }


        private void DetectNPCHits(){if(_chassisCollider==null||Mathf.Abs(_driveSpeed)<.15f)return;HashSet<NPCWanderer> current=_npcContactScratch;current.Clear();float halfWidth=_chassisCollider.size.x*.5f+bumperSideMargin;float frontZ=_chassisCollider.center.z+_chassisCollider.size.z*.5f;float rearZ=_chassisCollider.center.z-_chassisCollider.size.z*.5f;bool forward=_driveSpeed>=0;foreach(NPCWanderer npc in NPCWanderer.ActiveNpcs){if(npc==null||npc.IsDown)continue;Vector3 local=transform.InverseTransformPoint(npc.transform.position);float lateral=Mathf.Abs(local.x-_chassisCollider.center.x);if(lateral>halfWidth)continue;float longitudinal=forward?local.z-frontZ:rearZ-local.z;if(longitudinal<-.30f||longitudinal>bumperReach)continue;current.Add(npc);if(_npcHitThisContact.Contains(npc))continue;float kmh=SpeedKmh;if(npc.HitByVehicle(DriveVelocity,transform.position)){ApplyNPCImpactSpeedResponse(kmh);_npcHitThisContact.Add(npc);Debug.Log($"[CYDOY] BUMPER HIT: {npc.name} gap={longitudinal:F2}m side={lateral:F2}m speed={kmh:F1}km/h",this);}}_npcHitThisContact.RemoveWhere(n=>n==null||!current.Contains(n));}
        private void DetectNPCOverrun(){if(_chassisCollider==null||Mathf.Abs(_driveSpeed)<1f)return;HashSet<NPCWanderer> current=_npcOverrunScratch;current.Clear();float halfWidth=_chassisCollider.size.x*.5f*.92f,halfLength=_chassisCollider.size.z*.5f*1.05f;foreach(NPCWanderer npc in NPCWanderer.ActiveNpcs){if(npc==null||!npc.IsDown)continue;Vector3 local=transform.InverseTransformPoint(npc.DownPosition);bool underneath=Mathf.Abs(local.x-_chassisCollider.center.x)<=halfWidth&&Mathf.Abs(local.z-_chassisCollider.center.z)<=halfLength&&Mathf.Abs(local.y-_chassisCollider.center.y)<2f*_modelScale;if(!underneath)continue;current.Add(npc);if(_npcOverrunContact.Contains(npc))continue;float side=Mathf.Sign(local.x-_chassisCollider.center.x);if(Mathf.Abs(side)<.01f)side=Random.value>.5f?1f:-1f;Vector3 angular=_rb.angularVelocity;angular+=transform.forward*(side*overrunRollKick);angular.x=Mathf.Clamp(angular.x,-.5f,.5f);angular.z=Mathf.Clamp(angular.z,-.5f,.5f);_rb.angularVelocity=angular;Vector3 velocity=_rb.linearVelocity;velocity.y=Mathf.Min(velocity.y+overrunVerticalKick,.08f);_rb.linearVelocity=velocity;_npcOverrunContact.Add(npc);Debug.Log($"[CYDOY] NPC OVERRUN: {npc.name} side={(side<0?"left":"right")}",this);}_npcOverrunContact.RemoveWhere(n=>n==null||!current.Contains(n));}
        private void ApplyNPCImpactSpeedResponse(float kmh){if(kmh<=fullStopBelowKmh){_driveSpeed=0;_yawRate=0;Vector3 v=_rb.linearVelocity;v.x=0;v.z=0;_rb.linearVelocity=v;}else if(kmh<heavyBrakeBelowKmh)_driveSpeed*=mediumImpactSpeedRetention;}
        public float DistanceFrom(Vector3 p)
        {
            // WheelCollider.ClosestPoint can report unstable results after suspension
            // movement. Vehicle interaction must only use the stable chassis volume.
            if(_chassisCollider!=null&&_chassisCollider.enabled)return Vector3.Distance(p,_chassisCollider.ClosestPoint(p));
            return Vector3.Distance(p,transform.position);
        }
        public bool TryEnter(Transform player){if(_occupied||player==null||DistanceFrom(player.position)>interactionDistance)return false;_driver=player;_driverController=player.GetComponent<CharacterController>();_networkController=player.GetComponent<CheatOnYourDayOnes.Player.NetworkPlayerController>();_interactor=player.GetComponent<VehicleInteractor>();if(_networkController!=null)_networkController.enabled=false;if(_interactor!=null)_interactor.enabled=false;_driverRenderers=player.GetComponentsInChildren<Renderer>(true);_driverRendererStates=new bool[_driverRenderers.Length];for(int i=0;i<_driverRenderers.Length;i++){_driverRendererStates[i]=_driverRenderers[i].enabled;_driverRenderers[i].enabled=false;}_driverColliders=player.GetComponentsInChildren<Collider>(true);_driverColliderStates=new bool[_driverColliders.Length];for(int i=0;i<_driverColliders.Length;i++){_driverColliderStates[i]=_driverColliders[i].enabled;_driverColliders[i].enabled=false;}if(_driverController!=null)_driverController.enabled=false;Transform seat=driverSeat!=null?driverSeat:transform;player.SetPositionAndRotation(seat.position,seat.rotation);_camera=Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);if(_camera!=null)_camera.EnterVehicleMode(transform);ConfigureRigidbody();if(_physicsWheels.Count<4){DetectWheelsAndScale();RebuildVehicleColliders();}_driveSpeed=_throttle=_steer=_yawRate=0;_rb.linearVelocity=Vector3.zero;_rb.angularVelocity=Vector3.zero;_rb.WakeUp();_occupied=true;_ignoreExitUntilEReleased=true;_npcHitThisContact.Clear();_npcOverrunContact.Clear();Debug.Log("[CYDOY] VEHICLE READY - four-wheel suspension",this);return true;}
        public void Exit(){if(!_occupied||_driver==null)return;Transform p=_driver;p.position=exitPoint!=null?exitPoint.position:transform.position-transform.right*1.8f+Vector3.up*.25f;p.rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);if(_camera!=null)_camera.ExitVehicleMode(p);if(_driverRenderers!=null)for(int i=0;i<_driverRenderers.Length;i++)if(_driverRenderers[i]!=null)_driverRenderers[i].enabled=_driverRendererStates[i];if(_driverColliders!=null)for(int i=0;i<_driverColliders.Length;i++)if(_driverColliders[i]!=null)_driverColliders[i].enabled=_driverColliderStates[i];if(_driverController!=null)_driverController.enabled=true;if(_networkController!=null)_networkController.enabled=true;if(_interactor!=null)_interactor.enabled=true;_driver=null;_occupied=false;_rolloverActive=false;_rawThrottle=_rawSteer=_throttle=_steer=_driveSpeed=_yawRate=_visualSteeringInput=_rolloverRisk=_rolloverBuild=_rolloverEnergy=_postRolloverRestTime=0;_brake=false;_npcHitThisContact.Clear();_npcOverrunContact.Clear();}
        private void RebuildVehicleColliders()
        {
            foreach(WheelPhysics old in _physicsWheels)if(old.collider!=null)Destroy(old.collider.gameObject);
            _physicsWheels.Clear();
            foreach(Collider c in GetComponentsInChildren<Collider>(true))
            {
                if(c==null||c==_chassisCollider||c is WheelCollider||_wheelSupportColliders.Contains(c as SphereCollider)||c.isTrigger)continue;
                c.enabled=false;
            }
            foreach(SphereCollider old in _wheelSupportColliders)if(old!=null)Destroy(old.gameObject);
            _wheelSupportColliders.Clear();
            Renderer[] renderers=GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return;
            var bodyRenderers=new List<Renderer>();
            foreach(Renderer renderer in renderers)
                if(renderer!=null&&!_wheelRenderers.Contains(renderer))bodyRenderers.Add(renderer);
            Bounds bodyBounds=CalculatePreciseLocalBounds(bodyRenderers.Count>0?bodyRenderers.ToArray():renderers);
            if(_chassisCollider==null){_chassisCollider=GetComponent<BoxCollider>();if(_chassisCollider==null)_chassisCollider=gameObject.AddComponent<BoxCollider>();}
            Vector3 raw=bodyBounds.size;
            _chassisCollider.size=new Vector3(raw.x*.96f,raw.y*.92f,raw.z*.96f);
            _chassisCollider.center=bodyBounds.center;
            _chassisCollider.enabled=true;
            _chassisCollider.isTrigger=false;
            _chassisCollider.material=ChassisMaterial();

            // Imported wheels often consist of two or three meshes. One physical support
            // per wheel pivot prevents those overlapping colliders from wobbling the car.
            Dictionary<Transform,Bounds> wheelGroups=new();
            foreach(Renderer wheel in _wheelRenderers)
            {
                if(wheel==null||!wheel.enabled)continue;
                Transform key=FindWheelSupportRoot(wheel.transform);
                if(wheelGroups.TryGetValue(key,out Bounds grouped)){grouped.Encapsulate(wheel.bounds);wheelGroups[key]=grouped;}
                else wheelGroups.Add(key,wheel.bounds);
            }
            float parentScale=Mathf.Max(.0001f,Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Max(Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z))));
            _wheelContactLocalY=float.PositiveInfinity;
            foreach(KeyValuePair<Transform,Bounds> pair in wheelGroups)
            {
                Bounds wb=pair.Value;
                float radiusWorld=Mathf.Clamp(wb.size.y*.5f,.13f*_modelScale,.48f*_modelScale);
                float contactLocalY=transform.InverseTransformPoint(wb.center-Vector3.up*radiusWorld).y;
                _wheelContactLocalY=Mathf.Min(_wheelContactLocalY,contactLocalY);
                GameObject support=new("CYDOY_WheelCollider_"+pair.Key.name);
                support.transform.SetParent(transform,false);
                support.transform.position=wb.center;
                support.transform.rotation=transform.rotation;
                WheelCollider wheelCollider=support.AddComponent<WheelCollider>();
                wheelCollider.radius=radiusWorld/parentScale;wheelCollider.mass=24f;
                wheelCollider.suspensionDistance=wheelSuspensionDistance*_modelScale/parentScale;
                wheelCollider.forceAppPointDistance=.12f*_modelScale/parentScale;
                wheelCollider.ConfigureVehicleSubsteps(5f,12,15);
                JointSpring spring=wheelCollider.suspensionSpring;spring.spring=wheelSpring;spring.damper=wheelDamper;spring.targetPosition=Mathf.Clamp01(suspensionTargetPosition);wheelCollider.suspensionSpring=spring;
                WheelFrictionCurve forward=wheelCollider.forwardFriction;forward.extremumSlip=.38f;forward.extremumValue=1f;forward.asymptoteSlip=.82f;forward.asymptoteValue=.72f;forward.stiffness=1.35f;wheelCollider.forwardFriction=forward;
                WheelFrictionCurve sideways=wheelCollider.sidewaysFriction;sideways.extremumSlip=.22f;sideways.extremumValue=1f;sideways.asymptoteSlip=.5f;sideways.asymptoteValue=.72f;sideways.stiffness=sidewaysTyreStiffness;wheelCollider.sidewaysFriction=sideways;
                Transform spin=pair.Key.name.ToLowerInvariant().Contains("spinpivot")?pair.Key:FindNamedAncestorOrChild(pair.Key,"spinpivot");
                Transform steering=spin!=null&&spin.parent!=null&&spin.parent.name.ToLowerInvariant().Contains("steeringpivot")?spin.parent:FindNamedAncestorOrChild(pair.Key,"steeringpivot");
                if(spin==null)spin=pair.Key;if(steering==null)steering=spin.parent!=null?spin.parent:spin;
                string wheelName=(steering.name+" "+spin.name).ToLowerInvariant();
                _physicsWheels.Add(new WheelPhysics{collider=wheelCollider,steeringPivot=steering,spinPivot=spin,steeringBaseRotation=steering.localRotation,spinBaseRotation=spin.localRotation,front=transform.InverseTransformPoint(wb.center).z>bodyBounds.center.z,left=transform.InverseTransformPoint(wb.center).x<bodyBounds.center.x});
            }
            VehicleWheelVisuals oldVisuals=GetComponent<VehicleWheelVisuals>();if(oldVisuals!=null)oldVisuals.enabled=false;
            if(!float.IsInfinity(_wheelContactLocalY)&&_chassisCollider!=null)
            {
                // Keep the underbody entirely away from road meshes. Curbs are handled by
                // the four ground probes; bumper-height obstacles use TallObstacleAtBumper.
                float minimumChassisBottom=_wheelContactLocalY+.28f*_modelScale;
                float currentBottom=_chassisCollider.center.y-_chassisCollider.size.y*.5f;
                if(currentBottom<minimumChassisBottom)
                {
                    float trim=minimumChassisBottom-currentBottom;
                    Vector3 size=_chassisCollider.size;
                    size.y=Mathf.Max(.25f*_modelScale,size.y-trim);
                    _chassisCollider.size=size;
                    Vector3 center=_chassisCollider.center;
                    center.y+=trim*.5f;
                    _chassisCollider.center=center;
                }
            }
            ConfigureRigidbody();
        }
        private static Transform FindNamedAncestorOrChild(Transform start,string token)
        {
            for(Transform current=start;current!=null;current=current.parent)if(current.name.ToLowerInvariant().Contains(token))return current;
            foreach(Transform child in start.GetComponentsInChildren<Transform>(true))if(child.name.ToLowerInvariant().Contains(token))return child;
            return null;
        }
        private Transform FindWheelSupportRoot(Transform wheel)
        {
            Transform fallback=wheel;
            for(Transform current=wheel;current!=null&&current!=transform;current=current.parent)
            {
                fallback=current;
                string n=current.name.ToLowerInvariant();
                if(n.Contains("wheel_")&&(n.Contains("spin")||n.Contains("steering")))return current;
            }
            return fallback;
        }
        private Bounds CalculatePreciseLocalBounds(Renderer[] renderers)
        {
            Vector3 min=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
            Vector3 max=new(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
            bool found=false;
            foreach(Renderer renderer in renderers)
            {
                if(renderer==null||!renderer.enabled)continue;
                Bounds local=renderer.localBounds;
                foreach(Vector3 corner in BoundsCorners(local))
                {
                    Vector3 rootLocal=transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    min=Vector3.Min(min,rootLocal);
                    max=Vector3.Max(max,rootLocal);
                    found=true;
                }
            }
            return found?new Bounds((min+max)*.5f,max-min):new Bounds(Vector3.zero,new Vector3(1.8f,1.2f,4f));
        }
        private static PhysicsMaterial TyreMaterial()
        {
            if(s_tyreMaterial!=null)return s_tyreMaterial;
            // The four supports share one Rigidbody and cannot roll independently. Low
            // surface friction prevents them behaving like locked brakes; lateral grip and
            // braking are already simulated explicitly in FixedUpdate.
            s_tyreMaterial=new PhysicsMaterial("CYDOY Rolling Tyre"){dynamicFriction=.08f,staticFriction=.1f,bounciness=0f,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
            return s_tyreMaterial;
        }
        private static PhysicsMaterial ChassisMaterial()
        {
            if(s_chassisMaterial!=null)return s_chassisMaterial;
            s_chassisMaterial=new PhysicsMaterial("CYDOY Chassis"){dynamicFriction=0f,staticFriction=0f,bounciness=0f,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
            return s_chassisMaterial;
        }
        private void PutTyresOnGround(){if(_wheelRenderers.Count==0)return;float bottom=float.PositiveInfinity;Vector3 avg=Vector3.zero;int count=0;foreach(Renderer wheel in _wheelRenderers){if(wheel==null||!wheel.enabled)continue;bottom=Mathf.Min(bottom,wheel.bounds.min.y);avg+=wheel.bounds.center;count++;}if(count==0)return;avg/=count;RaycastHit[] hits=Physics.RaycastAll(new Vector3(avg.x,avg.y+3*_modelScale,avg.z),Vector3.down,10*_modelScale,~0,QueryTriggerInteraction.Ignore);bool found=false;float ground=float.NegativeInfinity;foreach(RaycastHit hit in hits){if(hit.collider==null||hit.transform==transform||hit.transform.IsChildOf(transform)||hit.normal.y<.65f)continue;if(!found||hit.point.y>ground){ground=hit.point.y;found=true;}}if(!found)return;transform.position+=Vector3.up*(ground+tyreGroundClearance-bottom);Physics.SyncTransforms();}
        private static Vector3[] BoundsCorners(Bounds b){Vector3 min=b.min,max=b.max;return new[]{new Vector3(min.x,min.y,min.z),new Vector3(max.x,min.y,min.z),new Vector3(min.x,max.y,min.z),new Vector3(max.x,max.y,min.z),new Vector3(min.x,min.y,max.z),new Vector3(max.x,min.y,max.z),new Vector3(min.x,max.y,max.z),new Vector3(max.x,max.y,max.z)};}
    }
}
